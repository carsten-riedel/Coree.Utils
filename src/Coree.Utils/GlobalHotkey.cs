using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Coree.Utils
{
    public class GlobalHotkey
    {
        public const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private int id;
        private IntPtr handle;
        private Keys key;
        private KeyModifier modifier;

        public GlobalHotkey(Keys key, KeyModifier modifier, IntPtr handle)
        {
            this.key = key;
            this.modifier = modifier;
            this.handle = handle;
            this.id = this.GetHashCode();
        }

        public int Id
        {
            get { return id; }
        }

        public IntPtr Handle
        {
            get { return handle; }
        }

        public bool Register()
        {
            return RegisterHotKey(handle, id, (uint)modifier, (uint)key);
        }

        public bool Unregister()
        {
            return UnregisterHotKey(handle, id);
        }

        public bool Match(Message message)
        {
            return message.Msg == 0x0312 && message.WParam.ToInt32() == id;
        }
    }

    public enum KeyModifier
    {
        None = 0x0000,
        Alt = 0x0001,
        Ctrl = 0x0002,
        Shift = 0x0004,
        WinKey = 0x0008
    }
}
