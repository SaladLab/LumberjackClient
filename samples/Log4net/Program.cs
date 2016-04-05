using System;
using System.Collections.Generic;
using System.Threading;
using log4net;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Log4net.Logstash;

namespace Log4net.Sample
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Setup();
            TestLog();
            Thread.Sleep(1000);
        }

        private static void Setup()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();

            var patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%m";
            patternLayout.ActivateOptions();

            var logstashAppender = new LogstashAppender();
            logstashAppender.Host = "localhost";
            logstashAppender.Port = 5000;
            logstashAppender.Layout = patternLayout;
            logstashAppender.Fields = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("host", Environment.MachineName)
            };

            logstashAppender.ActivateOptions();
            log4net.Config.BasicConfigurator.Configure(logstashAppender);
        }

        private static void TestLog()
        {
            var logger = LogManager.GetLogger("Test");
            logger.Debug("Test Debug Log");
            logger.Info("Test Info Log");
            logger.Warn("Test Warn Log");
            logger.Error("Test Error Log", new Exception("Exception Message"));
        }
    }
}
