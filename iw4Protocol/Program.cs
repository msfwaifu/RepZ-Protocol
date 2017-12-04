using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace repzProtocol
{
    class Program
    {

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        public helpers helpers = new helpers();

        [STAThread]
        static void Main(string[] args)
        {
            //hide console application
            ShowWindow(Process.GetCurrentProcess().MainWindowHandle, SW_HIDE);

            //must be ran as administrator
            if (!_isAdministrator())
            {
                ProcessStartInfo iw4Protocol = new ProcessStartInfo();
                iw4Protocol.FileName = Assembly.GetExecutingAssembly().Location;
                iw4Protocol.Arguments = args.Length != 0 ? args[0] : "";
                iw4Protocol.Verb = "runas";
            again:
                try
                {
                    Process.Start(iw4Protocol);
                }
                catch
                {
                    goto again;
                }
                return;
            }

            //We don't want to have other process running while doing our stuff
            helpers.KillPriorProcess();

            RegistryKey key = Registry.ClassesRoot.OpenSubKey("repziw4m");
            //if no arguments are given, delete the registery and setup
            if (args.Length == 0)
            {

                //if we already have a registery delete it
                if (key != null)
                    Registry.ClassesRoot.DeleteSubKeyTree("repziw4m");
                
                OpenFileDialog iw4mSelector = new OpenFileDialog();

                iw4mSelector.FileName = "iw4m.exe";
                iw4mSelector.DefaultExt = ".exe";
                iw4mSelector.Filter = "Modern Warfare 2 Executable|iw4m.exe";
                iw4mSelector.FilterIndex = 2;
                iw4mSelector.InitialDirectory = "C:\\Program Files (x86)\\";
                iw4mSelector.RestoreDirectory = true;

                String iw4mDir = Directory.GetCurrentDirectory();
                if ( !File.Exists(iw4mDir + "\\iw4m.dll") && iw4mSelector.ShowDialog() == DialogResult.OK)
                {
                    if (File.Exists(iw4mSelector.FileName))
                    {
                        //Directory.SetCurrentDirectory(Path.GetDirectoryName(iw4mSelector.FileName));
                        iw4mDir = Path.GetDirectoryName(iw4mSelector.FileName);

                    }
                    else
                    {
                        MessageBox.Show("Could not find iw4m.exe, please make sure you chose the right folder");
                        return;
                    }
                }

                //check if we are in the right directory
                if (!File.Exists(iw4mDir + "\\iw4m.dll"))
                {
                    MessageBox.Show("Could not find iw4m.dll, please make sure you chose the right folder");
                    return;
                }

                iw4Protocol protocol = new iw4Protocol();
                if (protocol.RegisterProtocol(iw4mDir))
                {
                    if (File.Exists(iw4mDir + "\\" + Process.GetCurrentProcess().ProcessName + ".exe"))
                        File.Delete(iw4mDir + "\\" + Process.GetCurrentProcess().ProcessName + ".exe");

                    String fileName = String.Concat(Process.GetCurrentProcess().ProcessName, ".exe");
                    String filePath = Path.Combine(Environment.CurrentDirectory, fileName);
                    File.Copy(filePath, Path.Combine(iw4mDir, fileName));

                    MessageBox.Show("Protocol registered!");
                }
            
            //protocol registered, lets connect
            } else {

                string protocolIP = args[0].Remove(0,7);
                string server_ip = protocolIP.Replace("/", "").Replace("m:", "");
                RegistryKey gamepathkey = Registry.ClassesRoot.OpenSubKey("repziw4m");
                string gamepath = gamepathkey.GetValue("gamepath").ToString();

                //check if our gamepath has iw4m.exe (so if its still valid)
                if (!File.Exists(gamepath+"\\iw4m.exe"))
                {
                    MessageBox.Show("Could not find iw4m.exe, please re-register the iw4m protocol");
                    Registry.ClassesRoot.DeleteSubKeyTree("repziw4m");
                    return;
                }

                //check if server is full
                ServerQuery selectedServer = new ServerQuery( helpers.CreateIPEndPoint(server_ip) );
                if (selectedServer.serverMaxPlayers == 0)
                    MessageBox.Show("Error connecting to your friend, make sure the server is correctly setup.");

                if (selectedServer.serverMaxPlayers == selectedServer.serverNPlayers)
                {
                    MessageBox.Show("Server full, will auto join");
                    ShowWindow(Process.GetCurrentProcess().MainWindowHandle, SW_SHOW);
                    while (selectedServer.serverMaxPlayers == selectedServer.serverNPlayers)
                    {
                        System.Console.WriteLine("Trying to join... '"+selectedServer.serverName+"' (" + selectedServer.serverNPlayers.ToString() + " players of max " + selectedServer.serverMaxPlayers.ToString() + ")");
                        selectedServer.update();
                        System.Threading.Thread.Sleep(1000);
                    }
                }
                ShowWindow(Process.GetCurrentProcess().MainWindowHandle, SW_HIDE);

                String console_mp = gamepath + "\\m2demo\\console_mp.log";

                //start game if its not already running
                Process[] iw4m = Process.GetProcessesByName("iw4m");
                if (iw4m.Length == 0)
                {
                    if (File.Exists(console_mp))
                        File.Delete(console_mp);

                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.WorkingDirectory = gamepath;
                    startInfo.FileName = "iw4m.exe";
                    //startInfo.Arguments = "connect " + server_ip;
                    Process.Start(startInfo);
                    while (iw4m.Length == 0)
                    {
                        iw4m = Process.GetProcessesByName("iw4m");
                        System.Console.WriteLine("Waiting for game to start...");
                        System.Threading.Thread.Sleep(1000);
                    }
                }

                int retries = 30;
                while (!File.Exists(console_mp) && retries > 0)
                {
                    System.Console.WriteLine("Waiting for console_log to show up...");
                    retries--;
                    System.Threading.Thread.Sleep(1500);
                }

                if (retries == 0 && !File.Exists(console_mp))
                {
                    MessageBox.Show("Error: file '" + console_mp + "' does not exist");
                    return;
                }

                waitForGameStatus(console_mp, iw4m);
                //MessageBox.Show("Our game is ready");

                //game is ready send our connect command!
                System.Console.WriteLine("Communicating with the game...");

                //Lets query our game to join this server
                gameCommunicator gameCommunicator = new gameCommunicator();
                gameCommunicator.query( String.Format("connect {0}", server_ip) );

                //MessageBox.Show("Send message to game to connect to:" + server_ip);
            }
        }

        private static void waitForGameStatus(String console_mp, Process[] process)
        {
            
            Boolean gameReady = false;
            int Offset = 90 * 1024; //first few lines are bullshit for us anyways, why read it
            int ToRead = 9999 * 1024;//Selection of the file from our offset that we read
            //Read the log, wait for "Successfully loaded friends list."
            int retry = 0;
            FileStream fs = null;
            while (fs == null && retry <= 10)
            {

                try {

                    fs = new System.IO.FileStream(console_mp, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                    while (!fs.CanRead)
                    {
                        System.Threading.Thread.Sleep(1000);
                    }

                    while (!gameReady)
                    {
                        System.Console.WriteLine("Checking if game is ready...");
                        try
                        {
                            // Seek 1024 bytes from the begin of the file
                            fs.Seek(Offset, SeekOrigin.Begin);
                            // read 1024 bytes
                            byte[] bytes = new byte[ToRead];
                            fs.Read(bytes, 0, ToRead);
                            // Convert bytes to string
                            string s = Encoding.Default.GetString(bytes);
                            // and output
                            string[] lines = s.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                            foreach (string line in lines)
                            {
                                if (gameReady)
                                    break;

                                System.Console.WriteLine(line);
                                //MessageBox.Show(line);
                                //Match match = Regex.Match(line, @"(RPC_HandleMessage: type 1112)");
                                Match match = Regex.Match(line, @"(Dispatching RPC message with ID 5 and type 1112.)");
                                while (match.Success)
                                {
                                    gameReady = true;
                                    while (!process[0].Responding)
                                    {
                                        System.Threading.Thread.Sleep(10000);
                                    }
                                    break;
                                }
                            }

                        }
                        catch (Exception e)
                        {
                            MessageBox.Show("Error: " + e.ToString());
                        }
                    }
                    fs.Close();

                } catch (Exception ex) {

                    retry++;
                    if(retry == 10)
                        MessageBox.Show("Something went wrong while trying to read your game log... Report this incidident ("+ex.ToString()+")");

                    System.Threading.Thread.Sleep(1000);

                }
            }

        }

        private static bool _isAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    }
}
