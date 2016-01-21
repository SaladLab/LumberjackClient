using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace LumberjackClient
{
    public class LumberjackClient
    {
        private readonly LumberjackClientSettings _settings;
        private TcpClient _client;
        private Stream _stream;
        private int _sequence;

        private readonly object _sendLock = new object();
        private readonly byte[][] _sendBuffers;
        private int _sendBufferIndex;
        private int _sendBufferOffset;
        private int _sendBufferDataCount;
        private volatile bool _sendProcessing;
        private int _sendWaitingSequence;

        private readonly byte[] _receiveBuffer;
        private int _receiveBufferOffset;

        public LumberjackClient(LumberjackClientSettings settings)
        {
            _settings = settings;

            _sendBuffers = new byte[2][];
            _sendBuffers[0] = new byte[settings.SendBufferSize];
            _sendBuffers[1] = new byte[settings.SendBufferSize];

            _receiveBuffer = new byte[settings.ReceiveBufferSize];

            ClearBuffer();
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
            _client = new TcpClient();
            _stream = null;
            _client.BeginConnect(_settings.Host, _settings.Port, ConnectCallback, null);
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                _client.EndConnect(ar);
            }
            catch (Exception e)
            {
                // TODO: Retry?
                Console.Write(e);
                _client = null;
                return;
            }

            // when connected, start receving and send pended data

            _stream = _client.GetStream();

            ProcessRecv();

            lock (_sendLock)
            {
                if (_sendProcessing == false)
                    ProcessSend();
            }
        }

        private void Close()
        {
            if (_client != null)
            {
                _client.Close();
                _client = null;
            }

            _stream = null;
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
                ProcessSend();
        }

        private void ProcessSend()
        {
            if (_stream == null)
            {
                if (_client == null)
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
            _stream.BeginWrite(buffer, 0, bufferLength, ProcessSendCallback, null);
        }

        private void ProcessSendCallback(IAsyncResult ar)
        {
            try
            {
                _stream.EndWrite(ar);
            }
            catch (Exception e)
            {
                Console.WriteLine("ProcessSendCallback: " + e);
                return;
            }

            if (false)
            {
                lock (_sendLock)
                {
                    ProcessSendIfPossible();
                }
            }
        }

        private void ProcessRecv()
        {
            try
            {
                _stream.BeginRead(
                    _receiveBuffer, 0, _receiveBuffer.Length,
                    ProcessRecvCallback, null);
            }
            catch (Exception e)
            {
                Console.WriteLine("ProcessRecv: " + e);
                Close();
            }
        }

        private void ProcessRecvCallback(IAsyncResult ar)
        {
            try
            {
                var readed = _stream.EndRead(ar);
                if (readed == 0)
                {
                    Close();
                    return;
                }
                _receiveBufferOffset += readed;
            }
            catch (Exception e)
            {
                Console.WriteLine("ProcessRecvCallback: " + e);
                Close();
                return;
            }

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

            ProcessRecv();

            //Close();
        }
    }
}
