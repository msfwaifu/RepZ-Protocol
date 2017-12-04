using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace repzProtocol
{
    class gameCommunicator {

        private const int WM_SETTEXT = 0x000C;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_RETURN = 0x0D;

        public void query(String command)
        {
            command = Regex.Replace(command, @"[^A-Za-z0-9\.\:\ ]+", "");
            // retrieve Notepad main window handle
            IntPtr console = FindWindow("IW4 WinConsole", "IW4 Console");
            IntPtr game = FindWindow("IW4", "Modern Warfare 2");
            if (!console.Equals(IntPtr.Zero))
            {
                // retrieve Edit window handle of Notepad
                IntPtr edithWnd = FindWindowEx(console, IntPtr.Zero, "Edit", null);
                if (!edithWnd.Equals(IntPtr.Zero))
                {
                    SendMessage(edithWnd, WM_SETTEXT, IntPtr.Zero, new StringBuilder("disconnect"));
                    PostMessage(edithWnd, WM_KEYDOWN, VK_RETURN, 0);
                    SendMessage(edithWnd, WM_SETTEXT, IntPtr.Zero, new StringBuilder(command));
                    PostMessage(edithWnd, WM_KEYDOWN, VK_RETURN, 0);

                    //Switch to game
                    SetForegroundWindow(game);
                    return;
                }
            }
            MessageBox.Show("Game not loaded...");
        }

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(
            string lpClassName,
            string lpWindowName);

        [DllImport("User32.dll")]
        private static extern IntPtr FindWindowEx(
            IntPtr hwndParent,
            IntPtr hwndChildAfter,
            string lpszClass,
        string lpszWindows);
        [DllImport("User32.dll")]
        private static extern Int32 SendMessage(
            IntPtr hWnd,
            int Msg,
            IntPtr wParam,
        StringBuilder lParam);
        [DllImport("user32.dll")]
        public static extern IntPtr PostMessage(IntPtr edithWnd, int WM_KEYDOWN, int p1, int p2);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

    }
}