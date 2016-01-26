using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LumberjackClient
{
    public class LumberjackClient
    {
        private readonly LumberjackClientSettings _settings;
        private readonly IPEndPoint _endPoint;

#if USE_MOCK_SOCKET
        internal IMockSocket _socket;
        internal Func<IMockSocket> _socketFactory;
        internal Action<string> _writeTrace;
#else
        private Socket _socket;
#endif
        private bool _connected;
        private int _sequence;

        private SocketAsyncEventArgs _sendArgs;
        private SocketAsyncEventArgs _receiveArgs;

        private struct PendingData
        {
            public KeyValuePair<string, string>[] KeyValuePairs;
            public ManualResetEvent WaitHandle;
        }

        private readonly object _sendLock = new object();
        private readonly CircularBuffer _sendBuffer;
        private volatile bool _sendProcessing;
        private int _sendWaitingSequence;
        private int _sendBusyCount;
        private List<PendingData> _sendPendingData;

        private readonly byte[] _receiveBuffer;
        private int _receiveBufferOffset;

        public LumberjackClient(LumberjackClientSettings settings)
        {
            _settings = settings;

            // init buffers

            _sendBuffer = new CircularBuffer(settings.SendBufferSize);
            _receiveBuffer = new byte[settings.ReceiveBufferSize];

            ClearBuffer();

            // resolve endpoint

            var host = Dns.GetHostEntry(settings.Host);
            var addressList = host.AddressList;
            _endPoint = new IPEndPoint(addressList[addressList.Length - 1], _settings.Port);
        }

        private void ClearBuffer()
        {
            _sendBuffer.Work.Offset = LumberjackProtocol.WindowSizeFrameSize;
            _sendBuffer.Work.DataCount = 0;
            _sendBuffer.Work.LastSequence = 0;

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
                Trace($"OnConnectCompleted: Error={args.SocketError}");
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
            lock (_sendLock)
            {
                if (_sendProcessing)
                {
                    _sendProcessing = false;

                    // if there are sending data, move it to work buffer 
                    // to send it again when reconnected
                    _sendBuffer.PushFront();
                }
            }
        }

        public void Send(params KeyValuePair<string, string>[] kvs)
        {
            ManualResetEvent waitHandle = null;

            lock (_sendLock)
            {
                var sent = WriteSendBuffer(kvs);
                if (sent)
                {
                    ProcessSendIfPossible();
                    return;
                }

                // TODO: if the size of one message exceeds buffer, throw!

                switch (_settings.SendFull)
                {
                    case LumberjackClientSettings.SendFullPolicy.Throw:
                        throw new InvalidOperationException("Send buffer full!");

                    case LumberjackClientSettings.SendFullPolicy.Wait:
                        if (_sendPendingData == null)
                            _sendPendingData = new List<PendingData>();
                        waitHandle = new ManualResetEvent(false);
                        _sendPendingData.Add(new PendingData
                        {
                            KeyValuePairs = kvs,
                            WaitHandle = waitHandle
                        });
                        break;
                }
            }

            waitHandle?.WaitOne();
        }

        private bool WriteSendBuffer(params KeyValuePair<string, string>[] kvs)
        {
            var buf = _sendBuffer.Work.Buffer;
            var pos = _sendBuffer.Work.Offset;

            int sequence;
            int written;

            try
            {
                sequence = _sequence + 1;
                written = LumberjackProtocol.EncodeData(
                    new ArraySegment<byte>(buf, pos, buf.Length - pos), sequence, kvs);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            _sequence = sequence;
            _sendBuffer.Work.Offset = pos + written;
            _sendBuffer.Work.DataCount += 1;
            _sendBuffer.Work.LastSequence = sequence;

            return true;
        }

        private void ProcessSendIfPossible()
        {
            if (_sendProcessing == false && _sendBuffer.Work.DataCount > 0)
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

            // reset work buffer

            _sendBuffer.PopFront();
            _sendBuffer.Work.Offset = LumberjackProtocol.WindowSizeFrameSize;

            // if there are pending data, try to send it

            if (_sendPendingData != null)
            {
                while (_sendPendingData.Count > 0)
                {
                    var sent = WriteSendBuffer(_sendPendingData[0].KeyValuePairs);
                    if (sent)
                    {
                        _sendPendingData[0].WaitHandle.Set();
                        _sendPendingData.RemoveAt(0);
                    }
                    else
                    {
                        if (_sendBuffer.Work.DataCount == 0)
                        {
                            // this is an erroneous case because we cannot serialize one message at all.
                            // in this case, we cannot report exception to users so just drop this big data
                            Trace("IssueSend: TooBig");
                            _sendPendingData[0].WaitHandle.Set();
                            _sendPendingData.RemoveAt(0);
                        }
                        break;
                    }
                }
            }

            // send

            Trace($"IssueSend: SendSeq={_sendBuffer.Prev.LastSequence} DataCount={_sendBuffer.Prev.DataCount}");
            LumberjackProtocol.EncodeWindowSize(new ArraySegment<byte>(_sendBuffer.Prev.Buffer), _sendBuffer.Prev.DataCount);

            _sendProcessing = true;
            _sendWaitingSequence = _sendBuffer.Prev.LastSequence;

            _sendBusyCount = 2;
            _sendArgs.SetBuffer(_sendBuffer.Prev.Buffer, 0, _sendBuffer.Prev.Offset);
            if (_socket.SendAsync(_sendArgs) == false)
                OnSendComplete(null, _sendArgs);
        }

        private void OnSendComplete(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success)
            {
                Trace($"OnSendComplete: Error={args.SocketError}");
                Close();
                return;
            }

            var len = args.BytesTransferred;
            if (len != args.Count)
            {
                // TODO: reissue for left
                Trace($"OnSendComplete: SentPartial Len={len}");
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
                Trace($"IssueReceive: Exception={e}");
                Close();
            }
        }

        private void OnReceiveComplete(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success)
            {
                Trace($"OnReceiveComplete: Error={args.SocketError}");
                Close();
                return;
            }

            var len = args.BytesTransferred;
            if (len == 0)
            {
                Trace("OnReceiveComplete: Disconnected because len == 0");
                Close();
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

                    Trace($"OnReceiveComplete: Ack={sequence}");
                    if (_sendWaitingSequence <= sequence)
                    {
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
                    Trace($"OnReceiveComplete: Decode Exception={e}");
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
        }

        [Conditional("TRACE")]
        private void Trace(string log)
        {
#if USE_MOCK_SOCKET
            if (_writeTrace != null)
                _writeTrace(log);
            else
#endif
                Console.WriteLine(log);
        }
    }
}
