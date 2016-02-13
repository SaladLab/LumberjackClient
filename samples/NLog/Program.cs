using NLog.Config;
using NLog.Targets.Logstash;
using System;
using System.Threading;
using System.Collections.Generic;

namespace NLog
{
    class Program
    {
        static void Main(string[] args)
        {
            Setup();
            TestLog();
            Thread.Sleep(1000);
        }

        static void Setup()
        {
            var logstashTarget = new LogstashTarget();
            logstashTarget.Host = "localhost";
            logstashTarget.Port = 5000;
            logstashTarget.Header = "Header";
            logstashTarget.Layout = "${message}";
            logstashTarget.Footer = "Footer";
            logstashTarget.Fields = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("host", Environment.MachineName)
            };

            var config = new LoggingConfiguration();
            config.AddTarget("logstash", logstashTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, logstashTarget));
            LogManager.Configuration = config;
        }

        static void TestLog()
        {
            var logger = LogManager.GetLogger("Test");
            logger.Debug("Test Debug Log");
            logger.Info("Test Info Log");
            logger.Warn("Test Warn Log");
            logger.Error(new Exception("Exception Message"), "Test Error Log");
        }
    }
}
