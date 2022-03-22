using System.Collections.Generic;

namespace JPEG.Huffman
{
    internal class HuffmanTree
    {
        private LinkedList<HuffmanNode> nodes;

        public HuffmanNode BuildTree(int[] frequencies)
        {
            FillNodes(frequencies);

            while (nodes.Count > 1)
            {
                var firstMin = GetNodeWithMinFrequency();
                var secondMin = GetNodeWithMinFrequency();
                AddNodeAsDescending(new HuffmanNode
                {
                    Frequency = firstMin.Frequency + secondMin.Frequency,
                    Left = secondMin,
                    Right = firstMin
                });
            }

            return nodes.First?.Value;
        }

        private void FillNodes(int[] frequencies)
        {
            var list = new List<HuffmanNode>();
            for (var i = 0; i < byte.MaxValue + 1; i++)
            {
                if (frequencies[i] == 0)
                    continue;
                list.Add(new HuffmanNode { Frequency = frequencies[i], LeafLabel = (byte)i });
            }

            list.Sort((first, second) => first.CompareTo(second));
            nodes = new LinkedList<HuffmanNode>(list);
        }

        private void AddNodeAsDescending(HuffmanNode node)
        {
            if (nodes.Count == 0)
            {
                nodes.AddLast(node);
                return;
            }

            var wasInserted = false;
            var current = nodes.Last;
            do
            {
                if (current?.Value.Frequency >= node.Frequency)
                {
                    wasInserted = true;
                    nodes.AddAfter(current, node);
                    break;
                }

                current = current?.Previous;
            } while (current?.Previous != null);

            if (!wasInserted)
                nodes.AddFirst(node);
        }

        private HuffmanNode GetNodeWithMinFrequency()
        {
            var node = nodes.Last?.Value;
            nodes.RemoveLast();

            return node;
        }
    }
}