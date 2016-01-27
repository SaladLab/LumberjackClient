using System.Net;

namespace LumberjackClient
{
    public class LumberjackClientSettings
    {
        public enum SendFullPolicy
        {
            Drop,
            Throw,
            Wait,
        }

        public enum SendConfirmPolicy
        {
            Send,
            Receive,
        }

        public string Host;
        public int Port;
        public bool SslActive;
        public string SslThumbPrint;
        public int ConnectRetryCount = 10;
        public int SendBufferSize = 65536;
        public int ReceiveBufferSize = 4096;
        public SendFullPolicy SendFull;
        public SendConfirmPolicy SendConfirm;
    }
}
