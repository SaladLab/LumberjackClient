using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using LumberjackClient;

namespace Basic
{
    class Program
    {
        static void Main(string[] args)
        {
            var settings = new LumberjackClientSettings { Host = "localhost", Port = 5000 };
            var client = new LumberjackClient.LumberjackClient(settings);
            for (int i = 0; i < 100; i++)
            {
                client.Send(
                    new KeyValuePair<string, string>("host", Environment.MachineName),
                    new KeyValuePair<string, string>("message", "Test Message " + i),
                    new KeyValuePair<string, string>("key1", "value1"),
                    new KeyValuePair<string, string>("key2", "value2"),
                    new KeyValuePair<string, string>("key3", "value3"));
                if (i % 10 == 0)
                    Thread.Sleep(10);
            }

            Console.WriteLine("Done!");
            Thread.Sleep(1000000);
        }
    }
}
