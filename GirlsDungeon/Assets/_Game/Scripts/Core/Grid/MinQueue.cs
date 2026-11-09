using System.Collections.Generic;

namespace GirlsDungeon.Core.Grid
{
    /// <summary>우선순위 큐. 비용이 같으면 먼저 넣은 것이 먼저 나와 결과가 항상 같다.</summary>
    internal sealed class MinQueue
    {
        private struct Entry
        {
            public int Priority;
            public int Sequence;
            public int Value;
        }

        private readonly List<Entry> heap = new List<Entry>();
        private int nextSequence;

        public int Count => heap.Count;

        public void Enqueue(int value, int priority)
        {
            heap.Add(new Entry { Priority = priority, Sequence = nextSequence++, Value = value });
            int index = heap.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!IsLess(heap[index], heap[parent])) break;
                Swap(index, parent);
                index = parent;
            }
        }

        public int Dequeue()
        {
            int value = heap[0].Value;
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                int right = left + 1;
                int smallest = index;
                if (left < heap.Count && IsLess(heap[left], heap[smallest])) smallest = left;
                if (right < heap.Count && IsLess(heap[right], heap[smallest])) smallest = right;
                if (smallest == index) break;
                Swap(index, smallest);
                index = smallest;
            }
            return value;
        }

        private static bool IsLess(Entry a, Entry b)
        {
            return a.Priority != b.Priority ? a.Priority < b.Priority : a.Sequence < b.Sequence;
        }

        private void Swap(int a, int b)
        {
            var temp = heap[a];
            heap[a] = heap[b];
            heap[b] = temp;
        }
    }
}
