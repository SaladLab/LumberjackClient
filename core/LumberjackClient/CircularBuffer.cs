using System;

namespace LumberjackClient
{
    internal class CircularBuffer
    {
        public class Item
        {
            public byte[] Buffer;
            public int Offset;
            public int DataCount;
            public int LastSequence;
        }

        //         +------+------+------+
        // Send <- | Prev | Work | Next | <- Input
        //         +------+------+------+

        public Item Prev { get; private set; }
        public Item Work { get; private set; }
        public Item Next { get; private set; }

        public CircularBuffer(int bufferSize)
        {
            Prev = new Item {Buffer = new byte[bufferSize]};
            Work = new Item {Buffer = new byte[bufferSize]};
        }

        // discard Prev and move buffer left
        public void PopFront()
        {
            if (Next == null)
            {
                var prev = Prev;
                Prev = Work;
                Work = prev;
                Work.Offset = 0;
                Work.DataCount = 0;
            }
            else
            {
                Prev = Work;
                Work = Next;
                Next = null;
            }
        }

        // move buffer right
        public void PushFront()
        {
            if (Next != null)
                throw new InvalidOperationException("PushFront need Next empty!");

            Next = Work;
            Work = Prev;
            Prev = null;
        }
    }
}
