using System;
using System.Net.Sockets;

namespace LumberjackClient.Tests
{
    public class MockSocket : IMockSocket
    {
        public bool ConnectAsync(SocketAsyncEventArgs e)
        {
            Console.WriteLine("ConnectAsync");
            return true;
        }

        public void Close()
        {
            Console.WriteLine("Close");
        }

        public bool SendAsync(SocketAsyncEventArgs e)
        {
            Console.WriteLine("SendAsync");
            return true;
        }

        public bool ReceiveAsync(SocketAsyncEventArgs e)
        {
            Console.WriteLine("ReceiveAsync");
            return true;
        }
    }
}
