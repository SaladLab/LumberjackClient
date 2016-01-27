using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace LumberjackClient.Tests
{
    public class MockSocket : IMockSocket
    {
        private object _lock = new object();
        private List<Action<bool>> _pendings = new List<Action<bool>>();
        private SocketAsyncEventArgs _receiveArgs;

        public MockServer Server { get; }
        public bool Connected { get; private set; }

        public bool ConnectPending { get; set; }
        public bool SendPending { get; set; }
        public bool SendDoneAfterReceive { get; set; }
        public int SendBufferSize { get; set; }
        public bool ReceivePending { get; set; }

        public MockSocket()
        {
            Server = new MockServer();
            Server.Sent = OnReceive;
        }

        public bool ConnectAsync(SocketAsyncEventArgs e)
        {
            lock (_lock)
            {
                if (ConnectPending)
                {
                    _pendings.Add(succeeded =>
                    {
                        Connected = succeeded;
                        e.InvokeSetResults(succeeded ? SocketError.Success : SocketError.ConnectionRefused, 0, SocketFlags.None);
                        e.InvokeOnCompleted();
                    });
                    return true;
                }
                else
                {
                    e.SocketError = SocketError.Success;
                    Connected = true;
                    return false;
                }
            }
        }

        public void Close()
        {
            lock (_lock)
            {
                if (_receiveArgs != null)
                {
                    var receiveArgs = _receiveArgs;
                    _receiveArgs = null;
                    receiveArgs.InvokeSetResults(SocketError.ConnectionReset, 0, SocketFlags.None);
                    receiveArgs.InvokeOnCompleted();
                }

                var pendings = _pendings;
                _pendings = new List<Action<bool>>();

                Connected = false;

                foreach (var pending in pendings)
                    pending(false);
            }
        }

        public bool SendAsync(SocketAsyncEventArgs e)
        {
            lock (_lock)
            {
                if (SendPending)
                {
                    // for keep sent data safe, it should be copied to receive buffer.
                    var receiveBuffer = new byte[e.Count];
                    Array.Copy(e.Buffer, e.Offset, receiveBuffer, 0, e.Count);

                    _pendings.Add(succeeded =>
                    {
                        if (succeeded)
                        {
                            if (SendDoneAfterReceive)
                            {
                                Server.OnReceive(new ArraySegment<byte>(receiveBuffer));
                                e.InvokeSetResults(SocketError.Success, e.Count, SocketFlags.None);
                                e.InvokeOnCompleted();
                            }
                            else
                            {
                                e.InvokeSetResults(SocketError.Success, e.Count, SocketFlags.None);
                                e.InvokeOnCompleted();
                                Server.OnReceive(new ArraySegment<byte>(receiveBuffer));
                            }
                        }
                        else
                        {
                            e.InvokeSetResults(SocketError.ConnectionReset, 0, SocketFlags.None);
                            e.InvokeOnCompleted();
                        }
                    });

                    return true;
                }
                else
                {
                    e.InvokeSetResults(SocketError.Success, e.Count, SocketFlags.None);
                    Server.OnReceive(new ArraySegment<byte>(e.Buffer, e.Offset, e.Count));
                    return false;
                }
            }
        }

        public bool ReceiveAsync(SocketAsyncEventArgs e)
        {
            lock (_lock)
            {
                if (_receiveArgs != null)
                    throw new InvalidOperationException("ReceiveAsync should be called as chain-fashion.");

                _receiveArgs = e;
                return true;
            }
        }

        public void WaitForPendings(bool flushAll = false)
        {
            lock (_lock)
            {
                while (_pendings.Count > 0)
                {
                    // for preventing modifying container while enumerating

                    var pendings = _pendings;
                    _pendings = new List<Action<bool>>();

                    foreach (var pending in pendings)
                        pending(true);

                    if (flushAll == false)
                        break;
                }
            }
        }

        private void OnReceive(ArraySegment<byte> buffer)
        {
            lock (_lock)
            {
                Action<bool> handler = succeeded =>
                {
                    var receiveArgs = _receiveArgs;
                    _receiveArgs = null;

                    if (succeeded)
                    {
                        receiveArgs.InvokeSetResults(SocketError.Success, (int)buffer.Count, SocketFlags.None);
                        Array.Copy(buffer.Array, buffer.Offset, receiveArgs.Buffer, receiveArgs.Offset, buffer.Count);
                    }
                    else
                    {
                        receiveArgs.InvokeSetResults(SocketError.ConnectionReset, 0, SocketFlags.None);
                    }
                    receiveArgs.InvokeOnCompleted();
                };

                if (ReceivePending)
                {
                    _pendings.Add(handler);
                }
                else
                {
                    handler(true);
                }
            }
        }
    }
}
