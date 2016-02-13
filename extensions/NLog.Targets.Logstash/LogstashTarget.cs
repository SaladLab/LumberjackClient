using System;
using System.Collections.Generic;
using LumberjackClient;
using NLog.Config;

namespace NLog.Targets.Logstash
{
    [Target("Logstash")]
    public sealed class LogstashTarget : TargetWithLayoutHeaderAndFooter
    {
        private LumberjackClientSettings _settings;
        private LumberjackClient.LumberjackClient _client;

        [RequiredParameter]
        public string Host { get; set; }

        [RequiredParameter]
        public int Port { get; set; }

        public int ConnectRetryCount { get; set; } = 10;
        public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public int SendBufferSize { get; set; } = 65536;
        public int ReceiveBufferSize { get; set; } = 4096;
        public LumberjackClientSettings.SendFullPolicy SendFull { get; set; } = LumberjackClientSettings.SendFullPolicy.Drop;
        public LumberjackClientSettings.SendConfirmPolicy SendConfirm { get; set; } = LumberjackClientSettings.SendConfirmPolicy.Receive;

        public List<KeyValuePair<string, string>> Fields { get; set; }

        public LogstashTarget()
        {
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

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

            if (Header != null)
            {
                var kvs = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("@timestamp", DateTime.UtcNow.ToString("o")),
                    new KeyValuePair<string, string>("message", Header.Render(LogEventInfo.CreateNullEvent()))
                };

                if (Fields != null)
                    kvs.AddRange(Fields);

                _client.Send(kvs);
            }
        }

        protected override void CloseTarget()
        {
            if (Footer != null)
            {
                var kvs = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("@timestamp", DateTime.UtcNow.ToString("o")),
                    new KeyValuePair<string, string>("message", Footer.Render(LogEventInfo.CreateNullEvent()))
                };

                if (Fields != null)
                    kvs.AddRange(Fields);

                _client.Send(kvs);
            }

            _client.Close();

            base.CloseTarget();
        }

        protected override void Write(LogEventInfo logEvent)
        {
            var kvs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@timestamp", logEvent.TimeStamp.ToUniversalTime().ToString("o")),
                new KeyValuePair<string, string>("logger", logEvent.LoggerName),
                new KeyValuePair<string, string>("level", logEvent.Level.ToString()),
                new KeyValuePair<string, string>("message", Layout.Render(logEvent)),
            };

            if (logEvent.Exception != null)
                kvs.Add(new KeyValuePair<string, string>("exception", logEvent.Exception.ToString()));

            if (Fields != null)
                kvs.AddRange(Fields);

            _client.Send(kvs);
        }
    }
}
