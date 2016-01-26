using System;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace LumberjackClient.Tests
{
    public class MockServer
    {
        public int Sequence { get; private set; }
        public List<KeyValuePair<string, string>> KeyValues { get; private set; }
        public ITestOutputHelper Output;

        public Action<ArraySegment<byte>> Sent { get; set; }

        public MockServer()
        {
            KeyValues = new List<KeyValuePair<string, string>>();
        }

        public void OnReceive(ArraySegment<byte> buffer)
        {
            var buf = buffer.Array;
            var idx = buffer.Offset;
            var idxLast = buffer.Offset + buffer.Count;

            // decode window size
            int windowSize;
            idx += LumberjackProtocol.DecodeWindowSize(new ArraySegment<byte>(buf, idx, idxLast - idx), out windowSize);

            Output?.WriteLine($"MockServer.OnReceive: Get Data WindowSize={windowSize}");

            // decode data 'window size' times
            for (var i = 0; i < windowSize; i++)
            {
                int sequence;
                KeyValuePair<string, string>[] kvs;
                idx += LumberjackProtocol.DecodeData(new ArraySegment<byte>(buf, idx, idxLast - idx), out sequence, out kvs);

                Sequence = sequence;
                KeyValues.AddRange(kvs);
            }

            // send ACK
            var ackBuffer = new ArraySegment<byte>(new byte[LumberjackProtocol.AckFrameSize]);
            LumberjackProtocol.EncodeAck(ackBuffer, Sequence);
            Send(ackBuffer);

            Output?.WriteLine($"MockServer.OnReceive: Send ACK Sequence={Sequence}");
        }

        private void Send(ArraySegment<byte> buffer)
        {
            Sent?.Invoke(buffer);
        }
    }
}
