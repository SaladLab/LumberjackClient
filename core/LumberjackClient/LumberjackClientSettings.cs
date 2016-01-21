using System.Net;

namespace LumberjackClient
{
    public class LumberjackClientSettings
    {
        public string Host;
        public int Port;
        public bool SslActive;
        public string SslThumbPrint;
        public int SendBufferSize = 65536;
        public int ReceiveBufferSize = 4096;

        // drop, waitandthrow, waitanddrop
        public bool BlockSendIfOverflow;
        public bool HardAck;

    }
}
