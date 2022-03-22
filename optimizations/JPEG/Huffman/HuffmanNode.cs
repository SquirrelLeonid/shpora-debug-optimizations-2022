using System;

namespace JPEG.Huffman
{
    internal class HuffmanNode : IComparable
    {
        public byte? LeafLabel { get; set; }
        public int Frequency { get; set; }
        public HuffmanNode Left { get; set; }
        public HuffmanNode Right { get; set; }

        public int CompareTo(object obj)
        {
            var other = (HuffmanNode)obj;

            if (Frequency > other.Frequency)
                return -1;
            if (Frequency == other.Frequency)
                return 0;

            return 1;
        }
    }
}