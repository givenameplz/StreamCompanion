﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using osu_StreamCompanion.Code.Interfeaces;

namespace osu_StreamCompanion.Code.Core.Maps
{
    
    public class Msn : IDisposable
    {
        private IntPtr m_hwnd;
        private Dictionary<string,string> _osuStatus= new Dictionary<string, string>(); 
        private static WNDCLASS lpWndClass;
        private List<IMsnGetter> _msnHandlers;
        private const string lpClassName = "MsnMsgrUIManager";
        public bool Suspend { get; set; }
        protected virtual void OnMSNStringChanged()
        {
            if (Suspend)
                return;
            Task.Factory.StartNew<int>(() =>
            {
                for (int i = 0; i < _msnHandlers.Count; i++)
                {
                    _msnHandlers[i].SetNewMsnString(new Dictionary<string, string>(_osuStatus));
                }
                return 1;
            });
        }

        public Msn(List<IMsnGetter> msnHandlers)
        {
            _msnHandlers = msnHandlers;
            lpWndClass = new WNDCLASS
            {
                lpszClassName = lpClassName,
                lpfnWndProc = new WndProc(this.CustomWndProc)
            };

            ushort num = RegisterClassW(ref lpWndClass);
            int num2 = Marshal.GetLastWin32Error();
            if ((num == 0) && (num2 != 0x582))
            {
                throw new Exception("Could not register window class");
            }
            this.m_hwnd = CreateWindowExW(0, lpClassName, string.Empty, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowExW(uint dwExStyle, [MarshalAs(UnmanagedType.LPWStr)] string lpClassName, [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == 0x4a)
            {
                COPYDATASTRUCT copydatastruct =
                    (COPYDATASTRUCT)Marshal.PtrToStructure(lParam, typeof(COPYDATASTRUCT));
                string str = Marshal.PtrToStringUni(copydatastruct.lpData, copydatastruct.cbData / 2);
                string[] separator = new string[] { @"\0" };
                string[] sourceArray = str.Split(separator, StringSplitOptions.None);
                if (sourceArray[2] == "0")
                {
                    //_status = null;
                }
                else
                {
                    _osuStatus["artist"] = sourceArray[5];
                    _osuStatus["title"] = sourceArray[4];
                    _osuStatus["diff"] = sourceArray[7];
                    _osuStatus["status"] = sourceArray[3].Split(new[] {' '}, 2)[0];

                    OnMSNStringChanged();
                }
            }
            return DefWindowProcW(hWnd, msg, wParam, lParam);

        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (this.m_hwnd != IntPtr.Zero)
            {
                DestroyWindow(this.m_hwnd);
                this.m_hwnd = IntPtr.Zero;
            }

        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern ushort RegisterClassW([In] ref WNDCLASS lpWndClass);

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public Msn.WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
        }

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
