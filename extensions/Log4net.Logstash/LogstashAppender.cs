using System;
using System.Collections.Generic;
using LumberjackClient;
using log4net.Appender;
using log4net.Core;

namespace Log4net.Logstash
{
    public sealed class LogstashAppender : AppenderSkeleton
    {
        private LumberjackClientSettings _settings;
        private LumberjackClient.LumberjackClient _client;

        public string Host { get; set; }
        public int Port { get; set; }
        public int ConnectRetryCount { get; set; } = 10;
        public TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);
        public int SendBufferSize { get; set; } = 65536;
        public int ReceiveBufferSize { get; set; } = 4096;
        public LumberjackClientSettings.SendFullPolicy SendFull { get; set; } = LumberjackClientSettings.SendFullPolicy.Drop;
        public LumberjackClientSettings.SendConfirmPolicy SendConfirm { get; set; } = LumberjackClientSettings.SendConfirmPolicy.Receive;

        public List<KeyValuePair<string, string>> Fields { get; set; }

        public override void ActivateOptions()
        {
            base.ActivateOptions();

            _settings = new LumberjackClientSettings
            {
                Host = Host,
                Port = Port,
                ConnectRetryCount = ConnectRetryCount,
                CloseTimeout = CloseTimeout,
                SendBufferSize = SendBufferSize,
                ReceiveBufferSize = ReceiveBufferSize,
                SendFull = SendFull,
                SendConfirm = SendConfirm,
            };

            _client = new LumberjackClient.LumberjackClient(_settings);
        }

        protected override void OnClose()
        {
            _client.Close();

            base.OnClose();
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            var kvs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@timestamp", loggingEvent.TimeStamp.ToUniversalTime().ToString("o")),
                new KeyValuePair<string, string>("logger", loggingEvent.LoggerName),
                new KeyValuePair<string, string>("level", loggingEvent.Level.ToString()),
                new KeyValuePair<string, string>("message", RenderLoggingEvent(loggingEvent)),
            };

            if (loggingEvent.ExceptionObject != null)
                kvs.Add(new KeyValuePair<string, string>("exception", loggingEvent.ExceptionObject.ToString()));

            if (Fields != null)
                kvs.AddRange(Fields);

            _client.Send(kvs);
        }
    }
}
