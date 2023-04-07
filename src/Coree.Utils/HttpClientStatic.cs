using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Coree.Utils
{
    public class HttpClientStatic
    {
        public class HttpClientResponse
        {
            public int XRateOverallMaxPolling { get; set; }

            public HttpResponseMessage HttpResponseMessage { get; set; }

            public string HttpResponseMessageContent { get; set; }

            public Exception Exception { get; set; }
        }

        public class XRateGroup
        {
            public string Name { get; set; }
            public List<XRateItem> rules { get; set; } = new List<XRateItem>();

            public int GroupMaxPolling
            {
                get
                {
                    int result = 0;
                    foreach (var item in rules)
                    {
                        if (item.MaxPoll > result)
                        {
                            result = item.MaxPoll;
                        }
                    }
                    return result;
                }
            }

            public XRateGroup(string name, string multiplexrate)
            {
                this.Name = name;
                var single = multiplexrate.Split(',').ToList();
                foreach (var item in single)
                {
                    rules.Add(new XRateItem(item.Trim()));
                }
            }
        }

        public class XRateItem
        {
            public int RequestsPerTimeFrame { get; set; }

            public int RequestsLimitTimeFrame { get; set; }

            public int RequestsReset { get; set; }

            public int MaxPoll
            {
                get
                {
                    var x = RequestsLimitTimeFrame / RequestsPerTimeFrame;
                    return x*1000;
                }
            }

            public XRateItem(string singlxrate)
            {
                string[] single = singlxrate.Split(':');
                RequestsPerTimeFrame = System.Convert.ToInt32(single[0]);
                RequestsLimitTimeFrame = System.Convert.ToInt32(single[1]);
                RequestsReset = System.Convert.ToInt32(single[2]);
            }
        }

        public class XRate
        {
            public List<XRateGroup> XRateGroups { get; set; } = new List<XRateGroup>();
            public List<string> RulesNames { get; set; }
            public bool HasRules { get; set; }
            public List<KeyValuePair<string, IEnumerable<string>>> xrate { get; set; }

            public XRate(List<KeyValuePair<string, IEnumerable<string>>> keyValuePairs)
            {
                XRateGroups.Clear();
                xrate = keyValuePairs.Where(e => e.Key.StartsWith("X-Rate-Limit", StringComparison.InvariantCultureIgnoreCase)).ToList();

                if (xrate.Any(e => e.Key.IndexOf("rules", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    HasRules = true;
                    RulesNames = xrate.Where(e => e.Key.IndexOf("rules", StringComparison.OrdinalIgnoreCase) >= 0).Select(ex => ex.Value).SelectMany(x => x).ToList().Aggregate((a, b) => $"{a}, {b}").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                    foreach (var item in RulesNames)
                    {
                        var xratexx = keyValuePairs.Where(e => String.Compare(e.Key, $@"X-Rate-Limit-{item}", true) == 0).Select(ex => ex.Value).SelectMany(x => x).ToList().FirstOrDefault();

                        XRateGroups.Add(new XRateGroup(item, xratexx));
                    }
                }
            }

            public int MaximalPoolingTimeOfAllGroups
            {
                get
                {
                    int result = 0;
                    foreach (var item in XRateGroups)
                    {
                        if (item.GroupMaxPolling > result)
                        {
                            result = item.GroupMaxPolling;
                        }
                    }
                    return result;
                }
            }
        }

        public static HttpClientResponse Send(HttpMethod httpMethod, Uri baseAddress, string endpoint, object parameters, string requestHeaderAcceptMediaType = "application/json", AuthenticationHeaderValue requestHeaderAuthorizationValue = null, CookieCollection requestCookies = null)
        {
            var uriBuilder = new UriBuilder(baseAddress)
            {
                Path = endpoint
            };

            if (httpMethod == HttpMethod.Get && parameters.GetType() == typeof(Dictionary<string, string>))
            {
                uriBuilder.Query = new FormUrlEncodedContent((Dictionary<string, string>)parameters).ReadAsStringAsync().Result;
            }
            else if (httpMethod == HttpMethod.Get && !(parameters.GetType() == typeof(Dictionary<string, string>)))
            {
                throw new ArgumentException("HttpMethod.Get parameters should be always of type Dictionary<string, string>");
            }

            var requestUri = uriBuilder.ToString();

            var clientResponse = new HttpClientResponse();

            using (var handler = new HttpClientHandler())
            {
                if (requestCookies != null)
                {
                    handler.CookieContainer = new CookieContainer();
                    handler.CookieContainer.Add(baseAddress, requestCookies);
                }

                using (var client = new HttpClient(handler))
                {
                    using (var requestMessage = new HttpRequestMessage(httpMethod, requestUri))
                    {
                        if (requestHeaderAcceptMediaType != null)
                        {
                            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(requestHeaderAcceptMediaType));
                        }

                        if (requestHeaderAuthorizationValue != null)
                        {
                            requestMessage.Headers.Authorization = requestHeaderAuthorizationValue;
                        }

                        if (httpMethod == HttpMethod.Post && parameters.GetType() == typeof(Dictionary<string, string>))
                        {
                            requestMessage.Content = new FormUrlEncodedContent((Dictionary<string, string>)parameters);
                        }
                        else if (httpMethod == HttpMethod.Post && !(parameters.GetType() == typeof(Dictionary<string, string>)))
                        {
                            requestMessage.Content =  new StringContent(System.Text.Json.JsonSerializer.Serialize(parameters), UnicodeEncoding.UTF8, "application/json");
                        }

                        try
                        {
                            clientResponse.HttpResponseMessage = client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead).Result;
                        }
                        catch (Exception e)
                        {
                            clientResponse.Exception = e;
                        }

                        if (clientResponse.HttpResponseMessage != null)
                        {
                            if (clientResponse.HttpResponseMessage.IsSuccessStatusCode)
                            {
                                clientResponse.HttpResponseMessageContent = clientResponse.HttpResponseMessage.Content.ReadAsStringAsync().Result;
                            }
                            else
                            {
                                clientResponse.HttpResponseMessageContent = clientResponse.HttpResponseMessage.ReasonPhrase;
                            }
                        }
                    }
                }
            }

            return clientResponse;
        }

        public static HttpClientResponse Sendx(HttpMethod httpMethod, Uri baseAddress, string endpoint, object parameters, string requestHeaderAcceptMediaType = "application/json", AuthenticationHeaderValue requestHeaderAuthorizationValue = null, CookieCollection requestCookies = null, bool AutoXRateDelay = false)
        {
            UriBuilder uriBuilder = new UriBuilder(baseAddress)
            {
                Path = endpoint
            };
            if (httpMethod == HttpMethod.Get && parameters.GetType() == typeof(Dictionary<string, string>))
            {
                uriBuilder.Query = new FormUrlEncodedContent((Dictionary<string, string>)parameters).ReadAsStringAsync().Result;
            }
            else if (httpMethod == HttpMethod.Get && !(parameters.GetType() == typeof(Dictionary<string, string>)))
            {
                throw new ArgumentException("HttpMethod.Get parameters should be always of type Dictionary<string, string>");
            }

            string requestUri = uriBuilder.ToString();
            HttpClientResponse httpClientResponse = new HttpClientResponse();
            using (HttpClientHandler httpClientHandler = new HttpClientHandler())
            {
                if (requestCookies != null)
                {
                    httpClientHandler.CookieContainer = new CookieContainer();
                    httpClientHandler.CookieContainer.Add(baseAddress, requestCookies);
                }

                using (HttpClient httpClient = new HttpClient(httpClientHandler))
                {
                    using (HttpRequestMessage httpRequestMessage = new HttpRequestMessage(httpMethod, requestUri))
                    {
                        if (requestHeaderAcceptMediaType != null)
                        {
                            httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(requestHeaderAcceptMediaType));
                        }

                        if (requestHeaderAuthorizationValue != null)
                        {
                            httpRequestMessage.Headers.Authorization = requestHeaderAuthorizationValue;
                        }

                        if (httpMethod == HttpMethod.Post && parameters.GetType() == typeof(Dictionary<string, string>))
                        {
                            httpRequestMessage.Content = new FormUrlEncodedContent((Dictionary<string, string>)parameters);
                        }
                        else if (httpMethod == HttpMethod.Post && !(parameters.GetType() == typeof(Dictionary<string, string>)))
                        {
                            httpRequestMessage.Content = new StringContent(JsonSerializer.Serialize(parameters), Encoding.UTF8, "application/json");
                        }

                        try
                        {
                            httpClientResponse.HttpResponseMessage = httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead).Result;
                        }
                        catch (Exception ex)
                        {
                            Exception ex3 = (httpClientResponse.Exception = ex);
                        }

                        if (httpClientResponse.HttpResponseMessage != null)
                        {
                            if (httpClientResponse.HttpResponseMessage.IsSuccessStatusCode)
                            {
                                httpClientResponse.HttpResponseMessageContent = httpClientResponse.HttpResponseMessage.Content.ReadAsStringAsync().Result;
                            }
                            else
                            {
                                httpClientResponse.HttpResponseMessageContent = httpClientResponse.HttpResponseMessage.ReasonPhrase;
                            }
                        }

                        if (((int)httpClientResponse.HttpResponseMessage.StatusCode) == 429)
                        {
                            System.Threading.Thread.Sleep(System.Convert.ToInt32(httpClientResponse.HttpResponseMessage.Headers.RetryAfter.Delta.Value.TotalMilliseconds));
                            System.Threading.Thread.Sleep(System.Convert.ToInt32(4000));
                            return Send(httpMethod, baseAddress, endpoint, parameters, requestHeaderAcceptMediaType, requestHeaderAuthorizationValue, requestCookies);
                        }
                    }
                }
            }

            XRate xRate = new XRate(httpClientResponse.HttpResponseMessage.Headers.ToList());
            httpClientResponse.XRateOverallMaxPolling = xRate.MaximalPoolingTimeOfAllGroups;

            if (AutoXRateDelay)
            {
                System.Threading.Thread.Sleep(httpClientResponse.XRateOverallMaxPolling);
            }

            return httpClientResponse;
        }
    }
}