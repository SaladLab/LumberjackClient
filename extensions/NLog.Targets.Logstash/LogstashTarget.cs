using System.ComponentModel;
using System.Collections.Generic;
using LumberjackClient;
using NLog.Config;
using System;

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

        [DefaultValue(10)]
        public int ConnectRetryCount { get; set; } = 10;

        [DefaultValue(65536)]
        public int SendBufferSize { get; set; } = 65536;

        [DefaultValue(4096)]
        public int ReceiveBufferSize { get; set; } = 4096;

        [DefaultValue(LumberjackClientSettings.SendFullPolicy.Drop)]
        public LumberjackClientSettings.SendFullPolicy SendFull { get; set; } = LumberjackClientSettings.SendFullPolicy.Drop;

        [DefaultValue(LumberjackClientSettings.SendConfirmPolicy.Receive)]
        public LumberjackClientSettings.SendConfirmPolicy SendConfirm { get; set; } = LumberjackClientSettings.SendConfirmPolicy.Receive;

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
                SendBufferSize = SendBufferSize,
                ReceiveBufferSize = ReceiveBufferSize,
                SendFull = SendFull,
                SendConfirm = SendConfirm,
            };

            _client = new LumberjackClient.LumberjackClient(_settings);

            if (Header != null)
            {
                _client.Send(
                    new KeyValuePair<string, string>("host", Environment.MachineName),
                    new KeyValuePair<string, string>("message", Header.Render(LogEventInfo.CreateNullEvent())));
            }
        }

        protected override void CloseTarget()
        {
            if (Footer != null)
            {
                _client.Send(
                    new KeyValuePair<string, string>("host", Environment.MachineName),
                    new KeyValuePair<string, string>("message", Footer.Render(LogEventInfo.CreateNullEvent())));
            }
            // TODO: Dispose _client to flush all pending data
            base.CloseTarget();
        }

        protected override void Write(LogEventInfo logEvent)
        {
            _client.Send(
                new KeyValuePair<string, string>("logger", logEvent.LoggerName),
                new KeyValuePair<string, string>("level", logEvent.Level.ToString()),
                new KeyValuePair<string, string>("host", Environment.MachineName),
                new KeyValuePair<string, string>("message", Layout.Render(logEvent)));
        }
    }
}
