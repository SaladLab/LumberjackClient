using System;
using Xunit.Abstractions;

namespace LumberjackClient.Tests
{
    public class MockEnvironment
    {
        public ITestOutputHelper Output;
        public LumberjackClient Client;
        public MockSocket Socket;
        public MockServer Server;

        public static MockEnvironment Create(ITestOutputHelper output, Action<LumberjackClientSettings> settingsModifier = null, bool socketPending = false)
        {
            var clientSettings = new LumberjackClientSettings
            {
                Host = "localhost",
                Port = 5000,
                SendBufferSize = 1024,
                ReceiveBufferSize = 1024,
            };
            settingsModifier?.Invoke(clientSettings);

            var env = new MockEnvironment();
            env.Client = new LumberjackClient(clientSettings);
            env.Socket = new MockSocket();
            if (socketPending)
            {
                env.Socket.ConnectPending = true;
                env.Socket.SendPending = true;
                env.Socket.ReceivePending = true;
            }
            env.Server = env.Socket.Server;
            env.Server.Output = output;
            env.Client._socketFactory = () => env.Socket;
            env.Client._writeTrace = m => output?.WriteLine(m);
            return env;
        }
    }
}
