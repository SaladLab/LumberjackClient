using System;
using System.Collections.Generic;
using System.Text;

namespace LumberjackClient
{
    // https://github.com/elastic/logstash-forwarder/blob/master/PROTOCOL.md

    public static class LumberjackProtocol
    {
        public const byte Version = (byte)'1';

        public const int WindowSizeFrameSize = 6;
        public const int AckFrameSize = 6;

        public static int EncodeWindowSize(ArraySegment<byte> buffer, int windowSize)
        {
            if (buffer.Count < WindowSizeFrameSize)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            var buf = buffer.Array;
            var idx = buffer.Offset;

            // version '1'
            buf[idx + 0] = Version;

            // frametype: window size
            buf[idx + 1] = (byte)'W';

            // payload: 32bit unsigned window size value in units of whole data frames.
            buf[idx + 2] = (byte)(windowSize >> 24);
            buf[idx + 3] = (byte)(windowSize >> 16);
            buf[idx + 4] = (byte)(windowSize >> 8);
            buf[idx + 5] = (byte)(windowSize);

            return WindowSizeFrameSize;
        }

        public static int DecodeWindowSize(ArraySegment<byte> buffer, out int windowSize)
        {
            if (buffer.Count < AckFrameSize)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            var buf = buffer.Array;
            var idx = buffer.Offset;

            // version '1'
            var version = buf[idx];
            if (version != Version)
                throw new ArgumentException("Cannot decode frame. Version=" + version, nameof(buffer));

            // frametype: 'ack' frame type
            var type = buf[idx + 1];
            if (type != 'W')
                throw new ArgumentException("Cannot decode frame. Type=" + type, nameof(buffer));

            // payload: 32bit unsigned window size value in units of whole data frames.
            windowSize = (buf[idx + 2] << 24) | (buf[idx + 3] << 16) | (buf[idx + 4] << 8) | buf[idx + 5];

            return AckFrameSize;
        }

        public static int EncodeData(ArraySegment<byte> buffer, int sequence, IList<KeyValuePair<string, string>> kvs)
        {
            if (buffer.Count < 10)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            var buf = buffer.Array;
            var idx = buffer.Offset;
            var idxLast = buffer.Offset + buffer.Count;

            // version '1'
            buf[idx++] = Version;

            // frametype: data
            buf[idx++] = (byte)'D';

            // payload: 32bit unsigned sequence number
            buf[idx++] = (byte)(sequence >> 24);
            buf[idx++] = (byte)(sequence >> 16);
            buf[idx++] = (byte)(sequence >> 8);
            buf[idx++] = (byte)(sequence);

            // payload: 32bit 'pair' count
            var kvLength = kvs.Count;
            buf[idx++] = (byte)(kvLength >> 24);
            buf[idx++] = (byte)(kvLength >> 16);
            buf[idx++] = (byte)(kvLength >> 8);
            buf[idx++] = (byte)(kvLength);

            // payload: repeat key/value 'count' times
            foreach (var kv in kvs)
            {
                // payload: 32bit unsigned key length followed by that many bytes for the key
                var key = Encoding.UTF8.GetBytes(kv.Key);
                var keyLength = key.Length;
                if (idxLast - idx < keyLength + 4)
                    throw new ArgumentOutOfRangeException(nameof(buffer));
                buf[idx++] = (byte)(keyLength >> 24);
                buf[idx++] = (byte)(keyLength >> 16);
                buf[idx++] = (byte)(keyLength >> 8);
                buf[idx++] = (byte)(keyLength);
                Array.Copy(key, 0, buffer.Array, idx, keyLength);
                idx += keyLength;

                // payload: 32bit unsigned value length followed by that many bytes for the value
                var value = Encoding.UTF8.GetBytes(kv.Value);
                var valueLength = value.Length;
                if (idxLast - idx < valueLength + 4)
                    throw new ArgumentOutOfRangeException(nameof(buffer));
                buf[idx++] = (byte)(valueLength >> 24);
                buf[idx++] = (byte)(valueLength >> 16);
                buf[idx++] = (byte)(valueLength >> 8);
                buf[idx++] = (byte)(valueLength);
                Array.Copy(value, 0, buffer.Array, idx, valueLength);
                idx += valueLength;
            }

            return idx - buffer.Offset;
        }

        public static int DecodeData(ArraySegment<byte> buffer, out int sequence, out KeyValuePair<string, string>[] kvs)
        {
            if (buffer.Count < 10)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            var buf = buffer.Array;
            var idx = buffer.Offset;
            var idxLast = buffer.Offset + buffer.Count;

            // version '1'
            var version = buf[idx];
            if (version != Version)
                throw new ArgumentException("Cannot decode frame. Version=" + version, nameof(buffer));

            // frametype: 'ack' frame type
            var type = buf[idx + 1];
            if (type != 'D')
                throw new ArgumentException("Cannot decode frame. Type=" + type, nameof(buffer));

            // payload: 32bit unsigned sequence number
            sequence = (buf[idx + 2] << 24) | (buf[idx + 3] << 16) | (buf[idx + 4] << 8) | buf[idx + 5];

            // payload: 32bit 'pair' count
            var pairCount = (buf[idx + 6] << 24) | (buf[idx + 7] << 16) | (buf[idx + 8] << 8) | buf[idx + 9];
            kvs = new KeyValuePair<string, string>[pairCount];

            // payload: repeat key/value 'count' times
            idx = idx + 10;
            for (var i = 0; i < pairCount; i++)
            {
                var keyLength = (buf[idx] << 24) | (buf[idx + 1] << 16) | (buf[idx + 2] << 8) | buf[idx + 3];
                var key = Encoding.UTF8.GetString(buf, idx + 4, keyLength);
                if (idxLast - idx < keyLength + 4)
                    throw new ArgumentOutOfRangeException(nameof(buffer));
                idx += keyLength + 4;

                var valueLength = (buf[idx] << 24) | (buf[idx + 1] << 16) | (buf[idx + 2] << 8) | buf[idx + 3];
                var value = Encoding.UTF8.GetString(buf, idx + 4, valueLength);
                if (idxLast - idx < keyLength + 4)
                    throw new ArgumentOutOfRangeException(nameof(buffer));
                idx += valueLength + 4;

                kvs[i] = new KeyValuePair<string, string>(key, value);
            }

            return idx - buffer.Offset;
        }

        public static int EncodeAck(ArraySegment<byte> buffer, int sequence)
        {
            if (buffer.Count < WindowSizeFrameSize)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            var buf = buffer.Array;
            var idx = buffer.Offset;

            // version '1'
            buf[idx + 0] = Version;

            // frametype: 'ack' frame type
            buf[idx + 1] = (byte)'A';

            // payload: 32bit unsigned sequence number.
            buf[idx + 2] = (byte)(sequence >> 24);
            buf[idx + 3] = (byte)(sequence >> 16);
            buf[idx + 4] = (byte)(sequence >> 8);
            buf[idx + 5] = (byte)(sequence);

            return AckFrameSize;
        }

        public static int DecodeAck(ArraySegment<byte> buffer, out int sequence)
        {
            if (buffer.Count < AckFrameSize)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            var buf = buffer.Array;
            var idx = buffer.Offset;

            // version '1'
            var version = buf[idx];
            if (version != Version)
                throw new ArgumentException("Cannot decode frame. Version=" + version, nameof(buffer));

            // frametype: 'ack' frame type
            var type = buf[idx + 1];
            if (type != 'A')
                    throw new ArgumentException("Cannot decode frame. Type=" + type, nameof(buffer));

            // payload: 32bit unsigned sequence number.
            sequence = (buf[idx + 2] << 24) | (buf[idx + 3] << 16) | (buf[idx + 4] << 8) | buf[idx + 5];

            return AckFrameSize;
        }
    }
}
