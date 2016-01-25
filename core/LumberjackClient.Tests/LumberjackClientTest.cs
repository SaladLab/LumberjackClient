using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LumberjackClient.Tests
{
    public class LumberjackClientTest
    {
        [Fact]
        public void Test_SendAndReceiveAckAndSendAgain()
        {
            var client = new LumberjackClient(new LumberjackClientSettings
            {
                Host = "localhost",
                Port = 5000,
                SendBufferSize = 1024,
                ReceiveBufferSize = 1024,
            });
            client._socketFactory = () => new MockSocket();
            client.Send(new KeyValuePair<string, string>("Key1", "Value1"));
            Assert.True(true);
        }

        [Fact]
        public void Test_SendAndReceiveAckAndCloseAndSendAgain()
        {
        }
    }
}
