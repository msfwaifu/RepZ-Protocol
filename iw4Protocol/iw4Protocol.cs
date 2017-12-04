using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace repzProtocol
{
    class iw4Protocol
    {
        public Boolean RegisterProtocol(string iw4Path)
        {
            RegistryKey key = Registry.ClassesRoot.OpenSubKey("repziw4m");
            
            if (key == null)
            {
                key = Registry.ClassesRoot.CreateSubKey("repziw4m");
                key.SetValue(string.Empty, "URL: repziw4m Protocol");
                key.SetValue("URL Protocol", string.Empty);
                key.SetValue("gamepath", iw4Path);

                key = key.CreateSubKey(@"shell\open\command");
                key.SetValue(string.Empty, iw4Path + "\\" + Process.GetCurrentProcess().ProcessName + " " + "%1");
                key.Close();
                return true;
            }
            return false;
        }

    }
}
