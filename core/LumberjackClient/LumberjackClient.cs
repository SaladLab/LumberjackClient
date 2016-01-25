using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LumberjackClient
{
    public class LumberjackClient
    {
        private readonly LumberjackClientSettings _settings;
        private IPEndPoint _endPoint;

#if USE_MOCK_SOCKET
        internal IMockSocket _socket;
        internal Func<IMockSocket> _socketFactory;
#else
        private Socket _socket;
#endif

        private bool _connected;
        private int _sequence;

        private SocketAsyncEventArgs _sendArgs;
        private SocketAsyncEventArgs _receiveArgs;

        private readonly object _sendLock = new object();
        private readonly byte[][] _sendBuffers;
        private int _sendBufferIndex;
        private int _sendBufferOffset;
        private int _sendBufferDataCount;
        private volatile bool _sendProcessing;
        private int _sendWaitingSequence;
        private int _sendBusyCount;

        private readonly byte[] _receiveBuffer;
        private int _receiveBufferOffset;

        public LumberjackClient(LumberjackClientSettings settings)
        {
            _settings = settings;

            // init buffers

            _sendBuffers = new byte[2][];
            _sendBuffers[0] = new byte[settings.SendBufferSize];
            _sendBuffers[1] = new byte[settings.SendBufferSize];

            _receiveBuffer = new byte[settings.ReceiveBufferSize];

            ClearBuffer();

            // resolve endpoint

            var host = Dns.GetHostEntry(settings.Host);
            var addressList = host.AddressList;
            _endPoint = new IPEndPoint(addressList[addressList.Length - 1], _settings.Port);
        }

        private void ClearBuffer()
        {
            _sendBufferIndex = 0;
            _sendBufferOffset = LumberjackProtocol.WindowSizeFrameSize;
            _sendBufferDataCount = 0;

            _sendProcessing = false;
            _sendWaitingSequence = 0;

            _receiveBufferOffset = 0;
        }

        private void Connect()
        {
#if USE_MOCK_SOCKET
            _socket = _socketFactory != null
                ? _socketFactory()
                : new WrappedMockSocket(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
#else
            _socket = new Socket(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
#endif
            _connected = false;

            var args = new SocketAsyncEventArgs();
            args.RemoteEndPoint = _endPoint;
            args.Completed += OnConnectCompleted;
            if (_socket.ConnectAsync(args) == false)
                OnConnectCompleted(null, args);
        }

        private void OnConnectCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success)
            {
                Console.Write("OnConnectCompleted: " + args.SocketError);
                _socket = null;
                // TODO: Retry?
                return;
            }

            // when connected, start receving and send pended data

            _connected = true;

            _sendArgs = new SocketAsyncEventArgs();
            _sendArgs.RemoteEndPoint = _endPoint;
            _sendArgs.Completed += OnSendComplete;

            _receiveArgs = new SocketAsyncEventArgs();
            _receiveArgs.RemoteEndPoint = _endPoint;
            _receiveArgs.Completed += OnReceiveComplete;

            IssueReceive();

            lock (_sendLock)
            {
                if (_sendProcessing == false)
                    IssueSend();
            }
        }

        private void Close()
        {
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }

            _connected = false;
        }

        public void Send(params KeyValuePair<string, string>[] kvs)
        {
            lock (_sendLock)
            {
                var buf = _sendBuffers[_sendBufferIndex];
                var pos = _sendBufferOffset;

                var sequence = ++_sequence;
                var written = LumberjackProtocol.EncodeData(
                    new ArraySegment<byte>(buf, pos, buf.Length - pos), sequence, kvs);

                _sendBufferOffset = pos + written;
                _sendBufferDataCount += 1;

                ProcessSendIfPossible();
            }
        }

        private void ProcessSendIfPossible()
        {
            if (_sendProcessing == false && _sendBufferDataCount > 0)
                IssueSend();
        }

        private void IssueSend()
        {
            if (_connected == false)
            {
                if (_socket == null)
                    Connect();
                return;
            }

            // switch buffer

            var buffer = _sendBuffers[_sendBufferIndex];
            var bufferLength = _sendBufferOffset;
            var bufferDataCount = _sendBufferDataCount;
            _sendBufferIndex = 1 - _sendBufferIndex;
            _sendBufferOffset = LumberjackProtocol.WindowSizeFrameSize;
            _sendBufferDataCount = 0;

            // send

            Console.WriteLine("Send: " + _sequence);
            LumberjackProtocol.EncodeWindowSize(new ArraySegment<byte>(buffer), bufferDataCount);

            _sendProcessing = true;
            _sendWaitingSequence = _sequence;

            _sendBusyCount = 2;
            _sendArgs.SetBuffer(buffer, 0, bufferLength);
            if (_socket.SendAsync(_sendArgs) == false)
                OnSendComplete(null, _sendArgs);
        }

        private void OnSendComplete(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success)
            {
                Console.WriteLine("OnSendComplete: " + args.SocketError);
                return;
            }

            var len = args.BytesTransferred;
            if (len != args.Count)
            {
                // TODO: reissue for left
                Console.WriteLine("OnSendComplete: !!! " + len);
                return;
            }

            lock (_sendLock)
            {
                _sendBusyCount -= 1;
                if (_sendBusyCount == 0)
                    ProcessSendIfPossible();
            }
        }

        private void IssueReceive()
        {
            try
            {
                _receiveArgs.SetBuffer(_receiveBuffer,
                                       _receiveBufferOffset,
                                       _receiveBuffer.Length - _receiveBufferOffset);
                if (_socket.ReceiveAsync(_receiveArgs) == false)
                    OnReceiveComplete(null, _receiveArgs);
            }
            catch (Exception e)
            {
                Console.WriteLine("IssueReceive: " + e);
                Close();
            }
        }

        private void OnReceiveComplete(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success)
            {
                Console.WriteLine("OnReceiveComplete: " + args.SocketError);
                return;
            }

            var len = args.BytesTransferred;
            if (len == 0)
            {
                Console.WriteLine("OnReceiveComplete: len == 0");
                return;
            }

            // try to deserialize incoming data

            _receiveBufferOffset += len;

            var bufPos = 0;
            while (_receiveBufferOffset - bufPos >= 6)
            {
                try
                {
                    int sequence;
                    var readed = LumberjackProtocol.DecodeAck(
                        new ArraySegment<byte>(_receiveBuffer, bufPos, _receiveBufferOffset - bufPos),
                        out sequence);
                    bufPos += readed;
                    Console.WriteLine("ACK:" + sequence);
                    if (_sendWaitingSequence <= sequence)
                    {
                        Console.WriteLine("  HIT:" + _sendWaitingSequence);

                        // TODO: Switch buffer
                        lock (_sendLock)
                        {
                            _sendProcessing = false;
                            _sendBusyCount -= 1;
                            if (_sendBusyCount == 0)
                                ProcessSendIfPossible();
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("DecodeAck: " + e);
                    Close();
                    return;
                }
            }

            var leftLen = _receiveBufferOffset - bufPos;
            if (leftLen > 0)
            {
                Array.Copy(_receiveBuffer, bufPos, _receiveBuffer, 0, leftLen);
                _receiveBufferOffset = leftLen;
            }
            else
            {
                _receiveBufferOffset = 0;
            }

            IssueReceive();

            //Close();
        }
    }
}
