#if USE_MOCK_SOCKET

using System.Net.Sockets;

namespace LumberjackClient
{
    public interface IMockSocket
    {
        bool ConnectAsync(SocketAsyncEventArgs e);
        void Close();
        bool SendAsync(SocketAsyncEventArgs e);
        bool ReceiveAsync(SocketAsyncEventArgs e);
    }

    public class WrappedMockSocket : IMockSocket
    {
        private readonly Socket _socket;

        public WrappedMockSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            _socket = new Socket(addressFamily, socketType, protocolType);
        }

        public bool ConnectAsync(SocketAsyncEventArgs e)
        {
            return _socket.ConnectAsync(e);
        }

        public void Close()
        {
            _socket.Close();
        }

        public bool SendAsync(SocketAsyncEventArgs e)
        {
            return _socket.SendAsync(e);
        }

        public bool ReceiveAsync(SocketAsyncEventArgs e)
        {
            return _socket.ReceiveAsync(e);
        }
    }
}

#endif
