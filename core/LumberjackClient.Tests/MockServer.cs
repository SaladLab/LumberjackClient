using System;
using System.Collections.Generic;

namespace LumberjackClient.Tests
{
    public class MockServer
    {
        public int LastSequence { get; private set; }
        public Action<ArraySegment<byte>> Sent { get; set; }

        public void OnReceive(ArraySegment<byte> buffer)
        {
            var buf = buffer.Array;
            var idx = buffer.Offset;
            var idxLast = buffer.Offset + buffer.Count;

            // decode window size
            int windowSize;
            idx += LumberjackProtocol.DecodeWindowSize(new ArraySegment<byte>(buf, idx, idxLast - idx), out windowSize);

            // decode data 'window size' times
            for (var i = 0; i < windowSize; i++)
            {
                int sequence;
                KeyValuePair<string, string>[] kvs;
                idx += LumberjackProtocol.DecodeData(new ArraySegment<byte>(buf, idx, idxLast - idx), out sequence, out kvs);

                LastSequence = sequence;
            }

            // send ACK
            var ackBuffer = new ArraySegment<byte>(new byte[LumberjackProtocol.AckFrameSize]);
            LumberjackProtocol.EncodeAck(ackBuffer, LastSequence);
            Send(ackBuffer);
        }

        private void Send(ArraySegment<byte> buffer)
        {
            Sent?.Invoke(buffer);
        }
    }
}
