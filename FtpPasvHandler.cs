using System;
using System.Net;
using System.Net.Sockets;

namespace IPCamFtpEvt
{
    class FtpPasvHandler
    {
        private Socket mSock = null;
        private byte[] mBuf = null;

        public FtpPasvHandler(Socket sock, byte[] buf)
        {
            this.mSock = sock;
            this.mBuf = buf;
        }

        public void Run()
        {
            var sock = this.mSock;
            var buf = this.mBuf;
            var totalBytes = (int)0;
            var n = (int)0;
            try
            {
                while (true)
                {
                    n = sock.Receive(buf);
                    if (n <= 0)
                    {
                        break;
                    }
                    totalBytes += n;
                }
                //
            }
            catch (Exception) {}
            sock.Close();
        }

        //
    }
}