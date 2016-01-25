using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LumberjackClient.Tests
{
    public class LumberjackProtocoltTest
    {
        [Fact]
        public void Test_EncodeWindowSize()
        {
            var buffer = new byte[LumberjackProtocol.WindowSizeFrameSize];
            var length = LumberjackProtocol.EncodeWindowSize(new ArraySegment<byte>(buffer), 0x01020304);
            Assert.Equal(LumberjackProtocol.WindowSizeFrameSize, length);
            Assert.Equal(
                new [] { LumberjackProtocol.Version, (byte)'W', (byte)0x01, (byte)0x02, (byte)0x03, (byte)0x04 }, 
                buffer);
        }

        [Fact]
        public void Test_DecodeWindowSize()
        {
            var buffer = new byte[LumberjackProtocol.WindowSizeFrameSize];
            var encodeLength = LumberjackProtocol.EncodeWindowSize(new ArraySegment<byte>(buffer), 0x01020304);

            int windowSize;
            var decodeLength = LumberjackProtocol.DecodeWindowSize(new ArraySegment<byte>(buffer), out windowSize);
            Assert.Equal(encodeLength, decodeLength);
            Assert.Equal(0x01020304, windowSize);
        }

        [Fact]
        public void Test_EncodeData()
        {
            var buffer = new byte[1024];
            var length = LumberjackProtocol.EncodeData(
                new ArraySegment<byte>(buffer),
                0x01020304,
                new KeyValuePair<string, string>("Key1", "Value1"),
                new KeyValuePair<string, string>("Key2", "Value2"));
            Assert.Equal(46, length);
            Assert.Equal(new byte[]
            {
                LumberjackProtocol.Version, (byte)'D',
                1, 2, 3, 4, 0, 0, 0, 2,
                0, 0, 0, 4, 75, 101, 121, 49,
                0, 0, 0, 6, 86, 97, 108, 117, 101, 49,
                0, 0, 0, 4, 75, 101, 121, 50,
                0, 0, 0, 6, 86, 97, 108, 117, 101, 50,
            }, buffer.Take(length).ToArray());
        }

        [Fact]
        public void Test_DecodeData()
        {
            var buffer = new byte[1024];
            var kvs = new[]
            {
                new KeyValuePair<string, string>("Key1", "Value1"),
                new KeyValuePair<string, string>("Key2", "Value2")
            };
            var encodeLength = LumberjackProtocol.EncodeData(new ArraySegment<byte>(buffer), 0x01020304, kvs);

            int sequence;
            KeyValuePair<string, string>[] kvs2;
            var decodeLength = LumberjackProtocol.DecodeData(new ArraySegment<byte>(buffer), out sequence, out kvs2);
            Assert.Equal(encodeLength, decodeLength);
            Assert.Equal(kvs, kvs2);
        }

        [Fact]
        public void Test_EncodeAck()
        {
            var buffer = new byte[LumberjackProtocol.AckFrameSize];
            var length = LumberjackProtocol.EncodeAck(new ArraySegment<byte>(buffer), 0x01020304);
            Assert.Equal(LumberjackProtocol.WindowSizeFrameSize, length);
            Assert.Equal(
                new[] { LumberjackProtocol.Version, (byte)'A', (byte)0x01, (byte)0x02, (byte)0x03, (byte)0x04 },
                buffer);
        }

        [Fact]
        public void Test_DecodeAck()
        {
            var buffer = new byte[LumberjackProtocol.AckFrameSize];
            var encodeLength = LumberjackProtocol.EncodeAck(new ArraySegment<byte>(buffer), 0x01020304);

            int sequence;
            var decodeLength = LumberjackProtocol.DecodeAck(new ArraySegment<byte>(buffer), out sequence);
            Assert.Equal(encodeLength, decodeLength);
            Assert.Equal(0x01020304, sequence);
        }
    }
}
