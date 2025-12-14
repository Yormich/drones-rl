using System;
using System.Collections.Generic;

namespace GridWorld.AI
{
    public class SimplePriorityQueue<T> where T : IComparable<T>
    {
        private readonly List<T> data;

        public SimplePriorityQueue()
        {
            this.data = new List<T>();
        }

        public int Count => data.Count;

        public void Enqueue(T item)
        {
            data.Add(item);
            int childIndex = data.Count - 1;

            // Bubble up
            while (childIndex > 0)
            {
                int parentIndex = (childIndex - 1) / 2;
                if (data[childIndex].CompareTo(data[parentIndex]) >= 0) break;

                // Swap
                T tmp = data[childIndex];
                data[childIndex] = data[parentIndex];
                data[parentIndex] = tmp;

                childIndex = parentIndex;
            }
        }

        public T Dequeue()
        {
            int lastIndex = data.Count - 1;
            T frontItem = data[0];
            data[0] = data[lastIndex];
            data.RemoveAt(lastIndex);

            lastIndex--;
            int parentIndex = 0;

            // Bubble down
            while (true)
            {
                int childIndex = parentIndex * 2 + 1;
                if (childIndex > lastIndex) break;

                int rightChild = childIndex + 1;
                if (rightChild <= lastIndex && data[rightChild].CompareTo(data[childIndex]) < 0)
                {
                    childIndex = rightChild;
                }

                if (data[parentIndex].CompareTo(data[childIndex]) <= 0) break;

                T tmp = data[parentIndex];
                data[parentIndex] = data[childIndex];
                data[childIndex] = tmp;

                parentIndex = childIndex;
            }
            return frontItem;
        }
    }
}