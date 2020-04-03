using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace IPCamFtpEvt
{
    class Program
    {
        private const int THREAD_STACK_SIZE = 0x8000;


        private static string gHttpTimeServer = "google.com";
        private static string gDataDirectory = string.Empty;
        private static int gFtpListenPort = 21000;
        private static int gPassiveModeListenPort = 21001;

        private static byte[] gDummyBuf = new byte [0x100000];
        private static long gInternetTimeDelta = 0;


        static void Main(string[] args)
        {
            Console.Title = "FTP Server for IPCAM event reporting";

            ParseCmdLnArgs(args);
            InitTimeBase();
            StartDummyFtpPasvServer();
            RunFtpServer();
        }

        private static void ApplyUserConfig(string key, string value)
        {
            if (key.Equals("FtpPort", StringComparison.OrdinalIgnoreCase))
            {
                gFtpListenPort = (int)ushort.Parse(value);
            }
            else if (key.Equals("FtpPassivePort", StringComparison.OrdinalIgnoreCase))
            {
                gPassiveModeListenPort = (int)ushort.Parse(value);
            }
            else if (key.Equals("TimeServer", StringComparison.OrdinalIgnoreCase))
            {
                gHttpTimeServer = value;
            }
            else if (key.Equals("DataDirectory", StringComparison.OrdinalIgnoreCase))
            {
                gDataDirectory = value;
            }
            //
        }

        private static void ParseCmdLnArgs(string[] args)
        {
            if (args.Length < 1)
            {
                return;
            }

            var fileName = args[0];
            var lines = File.ReadAllLines(fileName);
            foreach (var line in lines)
            {
                var pos = line.IndexOf(' ');
                if (pos < 0)
                {
                    continue;
                }

                var key = line.Substring(0, pos).Trim();
                var value = line.Substring(pos + 1);
                ApplyUserConfig(key, value);
            }
            //
        }

        private static void InitTimeBase()
        {
            var clock = new HttpGetTime(gHttpTimeServer);
            for (int i = 0; i < 120; ++i)
            {
                clock.GetTimeFromInternet();
                if (clock.IsDownloadOK)
                {
                    break;
                }
                Thread.Sleep(1000);
            }

            var t1 = clock.LocalTime;
            var t2 = clock.InternetTime;
            gInternetTimeDelta = (long)(t2 - t1).TotalSeconds;
        }

        private static void StartDummyFtpPasvServer()
        {
            var thread = new Thread(ThreadRunDummyFtpPasvServer, THREAD_STACK_SIZE);
            thread.IsBackground = true;
            thread.Start();
        }

        private static Socket CreateListenSock(int port)
        {
            var listenSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSock.Bind(new IPEndPoint(IPAddress.Any, port));
            listenSock.Listen(8);
            return listenSock;
        }

        private static void ThreadRunDummyFtpPasvServer()
        {
            var listenSock = CreateListenSock(gPassiveModeListenPort);
            while (true)
            {
                var sock = listenSock.Accept();
                if (null == sock)
                {
                    break;
                }

                var handler = new FtpPasvHandler(sock, gDummyBuf);
                var thread = new Thread(handler.Run, THREAD_STACK_SIZE);
                thread.IsBackground = true;
                thread.Start();
            }
            listenSock.Close();
        }

        private static void RunFtpServer()
        {
            var listenSock = CreateListenSock(gFtpListenPort);
            while (true)
            {
                var sock = listenSock.Accept();
                if (null == sock)
                {
                    break;
                }

                var handler = new FtpHandler(sock);
                handler.InternetTimeDelta = gInternetTimeDelta;
                handler.PassiveModeListenPort = gPassiveModeListenPort;
                handler.DataDirectory = gDataDirectory;

                var thread = new Thread(handler.Run, THREAD_STACK_SIZE);
                thread.IsBackground = true;
                thread.Start();
            }
            listenSock.Close();
        }

        //
    }
}
