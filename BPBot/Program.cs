using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Meebey.SmartIrc4net;
using Starksoft.Cryptography.OpenPGP;
using System.IO;
using System.Net;
using PasteBin;


namespace BPBot
{
    public class User
    {
        public string nick { get; set; }
        public string user { get; set; }
        public string kid { get; set; }
    }

    class Program
    {
        public static IrcClient irc = new IrcClient();
        public static GnuPG gpg = new GnuPG();

        public static List<User> loggedin = new List<User>();

        
      
        static void Main(string[] args)
        {
            
            irc.SendDelay = 200;
            irc.AutoRetry = true;
            irc.ChannelSyncing = true;
            irc.OnQueryMessage += new Meebey.SmartIrc4net.Delegates.MessageEventHandler(OnQueryMessage);
            irc.OnChannelMessage += new Meebey.SmartIrc4net.Delegates.MessageEventHandler(irc_OnChannelMessage);
            irc.OnInvite += new Meebey.SmartIrc4net.Delegates.InviteEventHandler(irc_OnInvite);
            gpg.BinaryPath = @"C:\Program Files (x86)\GNU\GnuPG\";

            if(irc.Connect("irc.freenode.net", 6667) == true) 
            {
                irc.User("BP-Bot", 0, "BitCoinPoliceBot");
                irc.Login("BP-Bot", "Bitcoin Police Bot");
                irc.Join("#bitcoin-police-bot");
                irc.Message(SendType.Message, "#bitcoin-police-bot", "hai");
                irc.Listen();
                irc.Disconnect();
            } 
            else 
            {
                System.Console.WriteLine("couldn't connect");
            }
        }

        static void irc_OnInvite(string inviter, string channel, Data ircdata)
        {
            irc.Join(channel);
            irc.Message(SendType.Message, channel, "Thanks for inviting me " + inviter + ". Please give me OPs so I can help you secure your channel");
        }

        static void irc_OnChannelMessage(Data ircdata)
        {
            if (ircdata.Message.StartsWith("#"))
            {
                string mes = ircdata.MessageEx[0].Substring(1);


                switch (mes)
                {
                    case "register":
                        register(ircdata);
                        break;
                    case "eauth":
                        login(ircdata, false);
                        break;
                    case "leauth":
                        login(ircdata, true);
                        break;
                    case "everify":
                        loginconf(ircdata);
                        break;
                    case "ident":
                        ident(ircdata);
                        break;
                    case "logout":
                        logout(ircdata);
                        break;
                    case "join":
                        join(ircdata);
                        break;
                    case "iqtest":
                        irc.Message(SendType.Message, ircdata.Channel, "IQ Test enabled by " + ircdata.Nick + ". Type /server iqtest to start");
                        break;
                    default:
                        irc.Message(SendType.Message, ircdata.Channel, "Unknown command " + ircdata.Nick);
                        break;
                }

            }
        }

        public static void OnQueryMessage(Data ircdata)
        {
            switch (ircdata.MessageEx[0])
            {
                case "join":
                    irc.Join(ircdata.MessageEx[1]);
                    break;
                case "part":
                    irc.Part(ircdata.MessageEx[1]);
                    break;
                case "say":
                    irc.Message(SendType.Message, ircdata.MessageEx[1], ircdata.MessageEx[2]);
                    break;
            }
        }

        public static void register(Data ircdata)
        {
            if ((ircdata.MessageEx[2].Length == 16) || (ircdata.MessageEx[2].Length == 8))
            {
                System.IO.StreamWriter sw = new System.IO.StreamWriter("accounts.txt", true);
                sw.WriteLine(ircdata.MessageEx[1] + " " + ircdata.MessageEx[2] + "\n");
                sw.Close();
                irc.Message(SendType.Message, ircdata.Channel, ircdata.Nick + ": Successfully registered business: " + ircdata.MessageEx[1] + " with GPG key: " + ircdata.MessageEx[2]);
            }
            else
            {
                irc.Message(SendType.Message, ircdata.Channel, ircdata.Nick + ": Invalid GPG key id.");
            }
        }

        public static void login(Data ircdata, bool wget)
        {

            string line;
            string name;
            if (ircdata.MessageEx.Count() > 1)
            {
                name = ircdata.MessageEx[1];
            }
            else
            {
                name = ircdata.Nick;
            }

            StreamReader file = new StreamReader("accounts.txt");
            while ((line = file.ReadLine()) != null)
            {
                
                string[] args = line.Split(' ');
                if (args[0] == name)
                {
                    gpg.Recipient = args[1];
                    MemoryStream unencrypted = new MemoryStream(Encoding.ASCII.GetBytes(args[0] + ":" + DateTime.Now.Ticks + "\n"));
                    MemoryStream encrypted = new MemoryStream();
                    gpg.Encrypt(unencrypted, encrypted);
                    Pastie p = new Pastie();
                    string pastie = p.SendViaPasteBin(StreamToString(encrypted), "#bitcoin-police gpg login request for: " + name);
                    string[] paste = pastie.Split('/');
                    if (wget)
                    {
                        irc.Message(SendType.Message, ircdata.Channel, ircdata.Nick + ": wget -qO http://pastebin.com/raw.php?i=" + paste[3] + " | gpg --decrypt");
                    }
                    else
                    {
                        irc.Message(SendType.Message, ircdata.Channel, ircdata.Nick + ": Request here: http://pastebin.com/raw.php?i=" + paste[3]);
                    }
                    System.IO.StreamWriter sw = new StreamWriter(name + "_challenge.txt");
                    sw.WriteLine(StreamToString(unencrypted));
                    sw.Close();
                }


            }
            file.Close();
                    
        }

        public static void loginconf(Data ircdata)
        {
            string[] arg = ircdata.MessageEx[1].Split(':');
            string name = arg[0];
            string line;
            System.IO.StreamReader file = new StreamReader("accounts.txt");
            while ((line = file.ReadLine()) != null)
            {
                string[] args = line.Split(' ');
                if (args[0] == name)
                {
                    StreamReader us = new StreamReader(name + "_challenge.txt");
                    string challenge = us.ReadLine();
                    us.Close();
                    if (challenge == ircdata.MessageEx[1])
                    {
                        User u = new User();
                        u.nick = ircdata.Nick;
                        u.user = args[0];
                        u.kid = args[1];
                        loggedin.Add(u);
                        irc.Message(SendType.Message, ircdata.Channel, ircdata.Nick + ": You are now logged in.");
                        System.IO.File.Delete(name + "_challenge.txt");
                    }
                    else
                    {
                        irc.Message(SendType.Message, ircdata.Channel, ircdata.Nick + ": Invalid login.");

                    }
                }

            }
            file.Close();

        }

        public static void ident(Data ircdata)
        {
            bool lin = false;

            foreach (User s in loggedin)
            {
                if (s.nick == ircdata.Nick)
                {
                    irc.Message(SendType.Message, ircdata.Channel, ircdata.Nick + ": you are logged in as " + s.user);
                    lin = true;
                }

            }
            if (!lin)
            {
                irc.Message(SendType.Message, ircdata.Channel, ircdata.Nick + ": you are not logged in.");
            }
        }

        public static void logout(Data ircdata)
        {
            int num = 0;
            bool lot = false;
            try
            {
                foreach (User s in loggedin)
                {
                    if (s.nick == ircdata.Nick)
                    {
                        loggedin.RemoveAt(num);
                        irc.Message(SendType.Message, ircdata.Channel, ircdata.Nick + ": you are now logged out");
                        lot = true;
                    }
                    num++;
                }
            }
            catch { }
            if (!lot)
            {
                irc.Message(SendType.Message, ircdata.Channel, ircdata.Nick + ": you are not logged in.");
            }

        }

        public static void join(Data ircdata)
        {
            if(admin(ircdata.Nick))
                irc.Join(ircdata.MessageEx[1]);
        }

        


        public static string StreamToString(MemoryStream ms)
        {
            ms.Seek(0, SeekOrigin.Begin);
            byte[] jsonBytes = new byte[ms.Length];
            ms.Read(jsonBytes, 0, (int)ms.Length);
            return Encoding.UTF8.GetString(jsonBytes);
        }

        public static bool admin(string nick)
        {
            if ((nick == "SomeoneWeird") || (nick == "MrTiggr") || (nick == "MrTiggr_") || (nick == "SomeoneWeirdTAFE"))
            {
                return true;
            }
            return false;
        }




    }
}
