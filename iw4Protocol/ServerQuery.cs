﻿using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Collections.Generic;

namespace repzProtocol
{

    public class ServerQuery
    {
        public enum ServerQueryStatus { Successful, TimedOut, Failed, NothingYet };
        public ServerQueryStatus queryStatus = ServerQueryStatus.NothingYet;

        protected IPEndPoint serverAddr; // most of these are just Dvars..
        public int serverNPlayers;   // but what the heck we'll organise them anyway.
        public int serverMaxPlayers;
        public int serverPing = 999; // classic old 999ing
        public string serverGametype;
        public string serverMap;
        public string serverMod;
        public string serverName;
        public List<Player> serverPlayerList;
        public Dictionary<string, string> serverDvars;
        public ListViewItem rowItem; // the GUI stores the ListViewItem unique to this class. 
        //Saves having to constantly loop through everything to find the correct one...!

        // UDP socket
        private UdpClient _udp;
        private byte[] status_packet;

        public ServerQuery(IPEndPoint addr)
        {
            this.serverAddr = addr;
            this.status_packet = chars2bytes("\xff\xff\xff\xffgetstatus".ToCharArray());
            update();
        }

        private byte[] chars2bytes(char[] chr) // using anything else appears to give dodgy bytes
        {
            byte[] b = new byte[chr.Length];
            for (int i = 0; i < chr.Length; i++)
                b[i] = (byte)chr[i];
            return b;
        }

        public void update()
        {
            try
            {
                _udp = new UdpClient();
                _udp.Client.ReceiveTimeout = 2000;
                _udp.Client.SendTimeout = 2000;

                _udp.Connect(serverAddr);

                int now = Environment.TickCount;
                _udp.Send(status_packet, status_packet.Length);

                IPEndPoint serverIn = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _udp.Receive(ref serverIn);
                this.serverPing = Environment.TickCount - now;

                if (queryStatus != ServerQueryStatus.Failed)
                {
                    if (!serverIn.Address.Equals(serverAddr.Address))
                    {
                        this.queryStatus = ServerQueryStatus.Failed;
                        throw new Exception("Got data from wrong host " +
                            serverIn.Address.ToString() + " expected from " +
                            serverAddr.Address.ToString());
                    }

                    this.parseAndPopulate(data);
                    this.queryStatus = ServerQueryStatus.Successful;
                }
                _udp.Close();
            }
            catch (SocketException)
            {
                this.queryStatus = ServerQueryStatus.TimedOut;
            }
            catch (Exception)
            {
                this.queryStatus = ServerQueryStatus.Failed;
            }
        }

        // Populate the class with the parsed packet data.
        private void parseAndPopulate(byte[] data)
        {
            string dataString = ASCIIEncoding.ASCII.GetString(data, 4, data.Length - 4);
            string[] tokData = dataString.Split('\\');
            Dictionary<string, string> dVars = new Dictionary<string, string>();
            for (int i = 1; i < tokData.Length; i += 2)
            {
                try
                {
                    dVars[tokData[i]] = tokData[i + 1];
                }
                catch
                { } // WHAT DO YOU MEAN THERE'S NO SECOND VALUE?
            }
            tokData = dataString.Split('\n');

            string[] pTok;
            List<Player> playerList = new List<Player>();
            for (int i = 2; i < tokData.Length; i++)
            {
                pTok = tokData[i].Split(' ');
                if (pTok.Length < 2)
                    continue;
                playerList.Add(
                    new Player(tokData[i].Split('"')[1],
                    int.Parse(pTok[0]),
                    int.Parse(pTok[1])));
            }

            // populate the class with what we've got.
            this.serverGametype = dVars["g_gametype"];
            this.serverMap = dVars["mapname"];
            this.serverMaxPlayers = int.Parse(dVars["sv_maxclients"]);
            this.serverNPlayers = playerList.Count;
            this.serverMod = "None";
            if (dVars.ContainsKey("fs_game"))
                this.serverMod = dVars["fs_game"];
            this.serverName = dVars["sv_hostname"];
            this.serverDvars = dVars;
            this.serverPlayerList = playerList;
        }

        public IPEndPoint ServerAddress
        {
            get
            {
                return serverAddr;
            }
        }

        public override bool Equals(object o)
        {
            try
            {
                return this.ServerAddress.ToString().Equals(((ServerQuery)o).ServerAddress.ToString());
            }
            catch
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return serverAddr.GetHashCode();
        }
    }

    // basic struct for a player in a server.
    public struct Player
    {
        public string name;
        public int score;
        public int ping;

        public Player(string name, int score, int ping)
        {
            this.name = name;
            this.score = score;
            this.ping = ping;
        }
    }
}