using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace LumberjackClient.Tests
{
    public class MockSocket : IMockSocket
    {
        private List<Action<bool>> _pendings = new List<Action<bool>>();
        private SocketAsyncEventArgs _receiveArgs;

        public MockServer Server { get; }
        public bool Connected { get; private set; }

        public bool ConnectPending { get; set; }
        public bool SendPending { get; set; }
        public bool SendDoneAfterReceive { get; set; }
        public bool ReceivePending { get; set; }

        public MockSocket()
        {
            Server = new MockServer();
            Server.Sent = OnReceive;
        }

        public bool ConnectAsync(SocketAsyncEventArgs e)
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

        public void Close()
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

            foreach (var pending in pendings)
                pending(false);

            Connected = false;
        }

        public bool SendAsync(SocketAsyncEventArgs e)
        {
            if (SendPending)
            {
                _pendings.Add(succeeded =>
                {
                    if (succeeded)
                    {
                        if (SendDoneAfterReceive)
                        {
                            Server.OnReceive(new ArraySegment<byte>(e.Buffer, e.Offset, e.Count));
                            e.InvokeSetResults(SocketError.Success, e.Count, SocketFlags.None);
                            e.InvokeOnCompleted();
                        }
                        else
                        {
                            e.InvokeSetResults(SocketError.Success, e.Count, SocketFlags.None);
                            e.InvokeOnCompleted();
                            Server.OnReceive(new ArraySegment<byte>(e.Buffer, e.Offset, e.Count));
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

        public bool ReceiveAsync(SocketAsyncEventArgs e)
        {
            if (_receiveArgs != null)
                throw new InvalidOperationException("ReceiveAsync should be called as chain-fashion.");

            _receiveArgs = e;
            return true;
        }

        public void WaitForPendings(bool flushAll = false)
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

        private void OnReceive(ArraySegment<byte> buffer)
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
