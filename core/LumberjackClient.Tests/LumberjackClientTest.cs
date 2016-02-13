using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace LumberjackClient.Tests
{
    public class LumberjackClientTest
    {
        private readonly ITestOutputHelper _output;

        public LumberjackClientTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Test_SimpleSendAndReceiveSynchrously()
        {
            var env = MockEnvironment.Create(_output);
            for (var i = 0; i < 10; i++)
            {
                env.Client.Send(new KeyValuePair<string, string>("Key" + i, "Value" + i));
            }
            Assert.Equal(10, env.Server.KeyValues.Count);
            Assert.Equal("Key9", env.Server.KeyValues[9].Key);
            Assert.Equal("Value9", env.Server.KeyValues[9].Value);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(false, 2)]
        [InlineData(false, 5)]
        [InlineData(true, 1)]
        [InlineData(true, 2)]
        [InlineData(true, 5)]
        public void Test_SimpleSendAndReceiveAsynchrously(bool sendDoneAfterReceive, int flushInterval)
        {
            var env = MockEnvironment.Create(_output, null, true);
            env.Socket.SendDoneAfterReceive = sendDoneAfterReceive;
            for (var i = 0; i < 10; i++)
            {
                env.Client.Send(new KeyValuePair<string, string>("Key" + i, "Value" + i));
                if ((i + 1) % flushInterval == 0)
                    env.Socket.WaitForPendings();
            }
            env.Socket.WaitForPendings(true);
            Assert.Equal(10, env.Server.KeyValues.Count);
            Assert.Equal("Key9", env.Server.KeyValues[9].Key);
            Assert.Equal("Value9", env.Server.KeyValues[9].Value);
        }

        [Fact]
        public void Test_SendFullPolicy_Drop()
        {
            var env = MockEnvironment.Create(_output,
                s =>
                {
                    s.SendFull = LumberjackClientSettings.SendFullPolicy.Drop;
                    s.SendBufferSize = 64;
                },
                true);

            env.Client.Send(new KeyValuePair<string, string>("Key0", "Value0"));
            env.Client.Send(new KeyValuePair<string, string>("Key1", "Value1"));
            env.Client.Send(new KeyValuePair<string, string>("Key2", "Value2")); // dropped
            env.Socket.WaitForPendings(true);

            Assert.Equal(2, env.Server.KeyValues.Count);
        }

        [Fact]
        public void Test_SendFullPolicy_Throw()
        {
            var env = MockEnvironment.Create(_output,
                s =>
                {
                    s.SendFull = LumberjackClientSettings.SendFullPolicy.Throw;
                    s.SendBufferSize = 64;
                },
                true);

            env.Client.Send(new KeyValuePair<string, string>("Key0", "Value0"));
            env.Client.Send(new KeyValuePair<string, string>("Key1", "Value1"));

            Assert.Throws<InvalidOperationException>(() =>
            {
                env.Client.Send(new KeyValuePair<string, string>("Key2", "Value2")); // throw
            });

            env.Socket.WaitForPendings(true);

            Assert.Equal(2, env.Server.KeyValues.Count);
        }

        [Theory]
        [InlineData(LumberjackClientSettings.SendConfirmPolicy.Send)]
        [InlineData(LumberjackClientSettings.SendConfirmPolicy.Receive)]
        public void Test_SendFullPolicy_Wait(LumberjackClientSettings.SendConfirmPolicy sendConfirm)
        {
            var env = MockEnvironment.Create(_output,
                s =>
                {
                    s.SendFull = LumberjackClientSettings.SendFullPolicy.Wait;
                    s.SendConfirm = sendConfirm;
                    s.SendBufferSize = 64;
                },
                true);

            var state = 0;
            ThreadPool.QueueUserWorkItem(o =>
            {
                while (state == 0)
                {
                    Thread.Sleep(0);
                    env.Socket.WaitForPendings(true);
                }
                state = 2;
            });

            var keyValues = new List<KeyValuePair<string, string>>();
            for (var i = 0; i < 10; i++)
            {
                var kv = new KeyValuePair<string, string>("Key" + i, "Value" + i);
                keyValues.Add(kv);
                env.Client.Send(kv);
            }

            state = 1;
            while (state == 1)
                Thread.Sleep(0);

            Assert.Equal(10, env.Server.KeyValues.Count);
            Assert.Equal(keyValues, env.Server.KeyValues);
        }

        [Fact]
        public void Test_SendAndReceiveAckAndCloseAndSendAgainWhenConnected()
        {
            var env = MockEnvironment.Create(_output, null, true);

            // make connected
            env.Client.Send(new KeyValuePair<string, string>("Key0", "Value0"));
            env.Socket.WaitForPendings();
            env.Socket.Close();

            // send more data that will be discarded by disconnection
            env.Client.Send(new KeyValuePair<string, string>("Key1", "Value1"));
            env.Client.Send(new KeyValuePair<string, string>("Key2", "Value2"));
            env.Socket.WaitForPendings(true);

            Assert.Equal(3, env.Server.KeyValues.Count);
        }

        [Fact]
        public void Test_ConnectRetry_Failed()
        {
            var env = MockEnvironment.Create(_output,
                s =>
                {
                    s.ConnectRetryCount = 2;
                },
                true);

            env.Client.Send(new KeyValuePair<string, string>("Key0", "Value0"));
            env.Socket.Close();
            env.Socket.Close();
            env.Socket.Close();
            Assert.Equal(null, env.Client._socket);
        }

        [Fact]
        public void Test_ConnectRetry_Succeeded()
        {
            var env = MockEnvironment.Create(_output,
                s =>
                {
                    s.ConnectRetryCount = 2;
                },
                true);

            env.Client.Send(new KeyValuePair<string, string>("Key0", "Value0"));
            env.Socket.Close();
            env.Socket.WaitForPendings(true);
            Assert.Equal(1, env.Server.KeyValues.Count);
        }

        [Fact]
        public void Test_Close_WaitForCompletingSend_But_ConnectTimeout()
        {
            var env = MockEnvironment.Create(_output,
                s =>
                {
                    s.CloseTimeout = TimeSpan.FromSeconds(0.1f);
                },
                true);

            env.Client.Send(new KeyValuePair<string, string>("Key0", "Value0"));
            env.Client.Close();

            Assert.Equal(0, env.Server.KeyValues.Count);
        }

        [Fact]
        public void Test_Close_WaitForCompletingSend_But_SendTimeout()
        {
            var env = MockEnvironment.Create(_output,
                s =>
                {
                    s.CloseTimeout = TimeSpan.FromSeconds(0.1f);
                },
                true);

            env.Client.Send(new KeyValuePair<string, string>("Key0", "Value0"));
            env.Socket.WaitForPendings(true);
            env.Client.Send(new KeyValuePair<string, string>("Key1", "Value1"));
            env.Client.Close();

            Assert.Equal(1, env.Server.KeyValues.Count);
        }

        [Theory]
        [InlineData(LumberjackClientSettings.SendConfirmPolicy.Send)]
        [InlineData(LumberjackClientSettings.SendConfirmPolicy.Receive)]
        public void Test_Close_WaitForCompletingSend_And_Completed(LumberjackClientSettings.SendConfirmPolicy sendConfirm)
        {
            var env = MockEnvironment.Create(_output,
                s =>
                {
                    s.SendConfirm = sendConfirm;
                    s.CloseTimeout = TimeSpan.FromSeconds(2);
                },
                true);

            env.Client.Send(new KeyValuePair<string, string>("Key0", "Value0"));
            env.Client.Send(new KeyValuePair<string, string>("Key1", "Value1"));

            var state = 0;
            ThreadPool.QueueUserWorkItem(o =>
            {
                while (state == 0)
                {
                    Thread.Sleep(0);
                    env.Socket.WaitForPendings(true);
                }
                state = 2;
            });

            env.Client.Close();

            state = 1;
            while (state == 1)
                Thread.Sleep(0);

            Assert.Equal(2, env.Server.KeyValues.Count);
        }
    }
}
