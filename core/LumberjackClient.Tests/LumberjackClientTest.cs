using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LumberjackClient.Tests
{
    public class LumberjackClientTest
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
        public void Test_DecodeAck()
        {
            var buffer = new[] {LumberjackProtocol.Version, (byte)'A', (byte)0x01, (byte)0x02, (byte)0x03, (byte)0x04};
            var sequence = 0;
            var length = LumberjackProtocol.DecodeAck(new ArraySegment<byte>(buffer), out sequence);
            Assert.Equal(LumberjackProtocol.AckFrameSize, length);
            Assert.Equal(0x01020304, sequence);
        }
    }
}
