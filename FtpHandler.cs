using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace IPCamFtpEvt
{
    class FtpHandler
    {
        private const int MAX_BUFFER_SIZE = 256;


        private static readonly object gLocker = new object();


        private DateTime mCreatedTime = DateTime.MinValue;        
        private Socket mSock = null;
        private string mLocalAddr = string.Empty;
        private string mRemoteAddr = string.Empty;
        private byte[] mBuf = null;


        public FtpHandler(Socket sock)
        {
            var now = DateTime.Now;
            this.mSock = sock;

            this.mCreatedTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc);
            this.mLocalAddr = ((IPEndPoint)sock.LocalEndPoint).Address.ToString();
            this.mRemoteAddr = ((IPEndPoint)sock.RemoteEndPoint).Address.ToString();
        }

        public int PassiveModeListenPort { get; set; }
        public long InternetTimeDelta { get; set; }
        public string DataDirectory { get; set;}

        public void Run()
        {
            try
            {
                this.ProcessNewFtpRequest();
            }
            catch (Exception) {}
            this.mSock.Close();
        }

        private void ProcessNewFtpRequest()
        {
            var sock = this.mSock;
            sock.NoDelay = true;

            this.SendSingleLine("220 FileZilla Server");
            
            this.mBuf = new byte [MAX_BUFFER_SIZE];
            var command = string.Empty;
            var commandParam = string.Empty;

            while (true)
            {
                command = string.Empty;
                if (!this.RecvFtpClientCommand(ref command, ref commandParam))
                {
                    break;
                }
                if (!this.ProcessFtpClientCommand(command, commandParam))
                {
                    break;
                }
            }
            //
        }

        private bool SendAll(byte[] buf, int offset, int len)
        {
            var sock = this.mSock;
            var actualLen = (int)0;
            var n = (int)0;
            while (actualLen < len)
            {
                n = sock.Send(buf, offset + actualLen, len - actualLen, SocketFlags.None);
                if (n <= 0)
                {
                    break;
                }
                actualLen += n;
            }
            return (actualLen == len);
        }

        private bool SendSingleLine(string s)
        {
            var buf = Encoding.ASCII.GetBytes(s + "\r\n");
            var result = this.SendAll(buf, 0, buf.Length);
            return result;
        }

        private string RecvSingleLine()
        {
            var sock = this.mSock;
            var buf = this.mBuf;
            var result = string.Empty;
            var maxLen = buf.Length;
            var n = (int)0;
            while (true)
            {
                n = sock.Receive(buf, 0, 1, SocketFlags.None);
                if (n <= 0)
                {
                    result = null;
                    break;
                }

                result += (char)buf[0];
                var len = result.Length;
                if (result.EndsWith("\r\n", StringComparison.Ordinal))
                {
                    result = result.Substring(0, len - 2);
                    break;
                }

                if (len > maxLen)
                {
                    result = null;
                    break;
                }
                //
            }
            return result;
        }

        private bool RecvFtpClientCommand(ref string command, ref string commandParam)
        {
            var s = this.RecvSingleLine();
            if (null == s)
            {
                return false;
            }

            var pos = s.IndexOf(' ');
            if (pos < 0)
            {
                command = s;
                commandParam = string.Empty;
            }
            else
            {                
                command = s.Substring(0, pos).ToUpper();
                commandParam = s.Substring(pos + 1);
            }
            return true;
        }

        private void SaveEvent(string eventFileName)
        {
            var internetTimeDelta = this.InternetTimeDelta;
            var eventTime = this.mCreatedTime.AddSeconds(internetTimeDelta);

            var dataDir = this.DataDirectory;
            var fileName = eventTime.ToString("yyyyMMdd");
            fileName = Path.Combine(dataDir, fileName + ".txt");

            var s = eventTime.ToString("yyyy-MM-dd HH:mm:ss");
            s += ", ";
            s += this.mRemoteAddr;
            s += ", ";
            s += eventFileName;
            s += "\r\n";

            var buf = Encoding.ASCII.GetBytes(s);
            lock (gLocker)
            {
                using (var f = File.Open(fileName, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    f.Write(buf, 0, buf.Length);
                    f.Flush();
                    f.Close();
                }
            }

            //Console.WriteLine(s);
        }

        private bool ProcessFtpClientCommand(string command, string commandParam)
        {
            if (command.Equals("QUIT", StringComparison.Ordinal))
            {
                this.SendSingleLine("221 Goodbye");
                return false;
            }
            else if (command.Equals("NOOP", StringComparison.Ordinal))
            {
                this.SendSingleLine("200 OK");
            }
            else if (command.Equals("USER", StringComparison.Ordinal))
            {
                var user = commandParam;
                var s = "331 Password required for " + user;
                this.SendSingleLine(s);
            }
            else if (command.Equals("PASS", StringComparison.Ordinal))
            {
                this.SendSingleLine("230 Logged on");
            }
            else if (command.Equals("PWD", StringComparison.Ordinal))
            {
                this.SendSingleLine("257 \"/\" is current directory.");
            }
            else if (command.Equals("TYPE", StringComparison.Ordinal))
            {
                var operType = commandParam;
                var s = "200 Type set to " + operType;
                this.SendSingleLine(s);
            }
            else if (command.Equals("PASV", StringComparison.Ordinal))
            {
                var s = "227 Entering Passive Mode (";                
                var sock = this.mSock;
                var ip = this.mLocalAddr;
                ip = ip.Replace('.', ',');
                s += ip;
                s += ',';

                var port = this.PassiveModeListenPort;
                s += (port >> 8).ToString();
                s += ',';
                s += (port & 0xff).ToString();
                s += ')';

                this.SendSingleLine(s);
            }
            else if (command.Equals("STOR", StringComparison.Ordinal))
            {
                var fileName = commandParam;
                try
                {
                    var s = "150 Opening data channel for file upload to server of \"/" + fileName + "\"";
                    this.SendSingleLine(s);
                    s = "226 Successfully transferred \"/" + fileName + "\"";
                    this.SendSingleLine(s);
                }
                catch (Exception) { }
                this.SaveEvent(fileName);
                return false;
            }
            else
            {
                this.SendSingleLine("200 OK");
            }
            return true;
        }



        //
    }
}