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
}

#endif
