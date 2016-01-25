using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LumberjackClient.Tests
{
    public class MockSocket : IMockSocket
    {
        private readonly List<Action> _pendings = new List<Action>();
        private readonly MockServer _server = new MockServer();
        private SocketAsyncEventArgs _receiveArg;

        public MockServer Server { get { return _server;  } }

        public MockSocket()
        {
            _server.Sent = OnSend;
        }

        public bool ConnectAsync(SocketAsyncEventArgs e)
        {
            Console.WriteLine("ConnectAsync");
            e.SocketError = SocketError.Success;
            return false;
        }

        public void Close()
        {
            Console.WriteLine("Close");
        }

        public bool SendAsync(SocketAsyncEventArgs e)
        {
            Console.WriteLine("SendAsync");

            e.SocketError = SocketError.Success;
            _server.OnReceive(new ArraySegment<byte>(e.Buffer, e.Offset, e.Count));
            // TODO: sent something decode
            // can pend sending sent message
            // can pend 
            return true;
        }

        public bool ReceiveAsync(SocketAsyncEventArgs e)
        {
            Console.WriteLine("ReceiveAsync");
            _receiveArg = e;
            return false;
        }

        public void WaitForPendings()
        {
            // ThreadPool.QueueUserWorkItem(WaitCallback)
        }

        private void OnSend(ArraySegment<byte> buffer)
        {
            _receiveArg.SocketError = SocketError.Success;
            _receiveArg.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);

            var methodForOnCompleted = _receiveArg.GetType().GetMethod("OnCompleted", BindingFlags.NonPublic | BindingFlags.Instance);
            methodForOnCompleted.Invoke(_receiveArg, new object[1] { _receiveArg });
        }
    }
}
