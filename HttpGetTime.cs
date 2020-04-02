using System;
using System.Text;
using System.Net.Sockets;

namespace IPCamFtpEvt
{
    class HttpGetTime
    {
        private const int HTTP_HEADER_LINE_MAX_LENGTH = 1000;


        private static readonly string[] MonthsInEnglish = new string[] {
            "January",
            "February",
            "March",
            "April",
            "May",
            "June",
            "July",
            "August",
            "September",
            "October",
            "November",
            "December"
        };


        private string mServer = string.Empty;
        private bool mDownloadOK = false;
        private bool mParseOK = false;
        private byte[] mBuf = null;
        private Socket mSock = null;
        private DateTime mLocalTime = DateTime.MinValue;
        private DateTime mInternetTime = DateTime.MinValue;


        public HttpGetTime(string server)
        {
            this.mServer = server;
        }

        public bool GetTimeFromInternet()
        {
            this.mDownloadOK = false;
            this.mParseOK = false;
            this.mLocalTime = DateTime.MinValue;

            var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.mSock = sock;
            try
            {
                this.GetTimeFromInternetSafely();
            }
            catch (Exception) { }
            sock.Close();
            this.mSock = null;

            return this.mDownloadOK;
        }

        public bool IsDownloadOK 
        {
            get
            {
                return this.mDownloadOK;
            }
        }

        public bool IsParseOK 
        {
            get
            {
                return this.mParseOK;
            }
        }

        public DateTime LocalTime
        {
            get
            {
                return this.mLocalTime;
            }
        }

        public DateTime InternetTime
        {
            get
            {
                return this.mInternetTime;
            }
        }

        private void GetTimeFromInternetSafely()
        {
            var sock = this.mSock;
            sock.Connect(this.mServer, 80);

            this.SendString("GET / HTTP/1.0\r\n\r\n");
            var now = DateTime.Now;
            this.mLocalTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc);

            this.mBuf = new byte [256];
            var lineCount = (int)0;
            var line = string.Empty;
            while (true)
            {
                line = this.RecvSingleLine();
                if (null == line)
                {
                    if (lineCount > 0)
                    {
                        this.mDownloadOK = true;
                    }
                    return;
                }
                ++lineCount;

                if (0 == line.Length)
                {
                    this.mDownloadOK = true;
                    return;
                }

                if (line.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
                {
                    this.mDownloadOK = true;
                    break;
                }
                //
            }

            var s = line.Substring(5).Trim();
            var pos = s.IndexOf(',');
            if (pos >= 0)
            {
                s = s.Substring(pos + 1).Trim();
            }

            var ar = s.Split(' ');
            if (ar.Length < 4)
            {
                return;
            }

            var day = (int)uint.Parse(ar[0]);
            var year = (int)uint.Parse(ar[2]);

            s = ar[1];
            var month = (int)-1;
            for (int i = 0; i < 12; ++i)
            {
                if (MonthsInEnglish[i].StartsWith(s, StringComparison.OrdinalIgnoreCase))
                {
                    month = i + 1;
                    break;
                }
            }
            if (month < 1 || month > 12)
            {
                return;
            }

            ar = ar[3].Split(':');
            if (ar.Length < 3)
            {
                return;
            }

            var hour = (int)uint.Parse(ar[0]);
            var minute = (int)uint.Parse(ar[1]);
            var second = (int)uint.Parse(ar[2]);

            this.mInternetTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
            this.mParseOK = true;
        }

        private string RecvSingleLine()
        {
            var sock = this.mSock;
            var buf = this.mBuf;
            var result = string.Empty;
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

                if (len > HTTP_HEADER_LINE_MAX_LENGTH)
                {
                    result = null;
                    break;
                }
                //
            }
            return result;
        }

        private bool SendString(string s)
        {
            var buf = Encoding.ASCII.GetBytes(s);
            return this.SendAll(buf, 0, buf.Length);
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



        //
    }
}