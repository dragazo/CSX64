using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSX64
{
    /// <summary>
    /// Represents a queue of fixed size where adding new items beyond the size of the queue removes the oldest items
    /// </summary>
    public sealed class OverflowQueue<T>
    {
        /// <summary>
        /// The raw container for the queue
        /// </summary>
        private T[] Data;
        /// <summary>
        /// The index of the first item in the queue
        /// </summary>
        private int Pos;
        /// <summary>
        /// The number of items in the queue
        /// </summary>
        public int Count { get; private set; }
        /// <summary>
        /// Gets the maximum capacity of the queue before adding subsequent items removes older items
        /// </summary>
        public int Capacity => Data.Length;

        /// <summary>
        /// Creates an overflow queue with the specified capacity
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public OverflowQueue(int cap)
        {
            // make sure capacity is valid
            if (cap <= 0) throw new ArgumentOutOfRangeException("Size of list must be greater than zero");

            Data = new T[cap];
            Pos = 0;
            Count = 0;
        }

        /// <summary>
        /// Gets the item at the specified index
        /// </summary>
        public T this[int index]
        {
            get => Data[(Pos + index) % Capacity];
            set => Data[(Pos + index) % Capacity] = value;
        }

        /// <summary>
        /// Adds an item to the queue
        /// </summary>
        public void Enqueue(T item)
        {
            // if we have enough room, just add the item
            if (Count < Capacity) Data[Count++] = item;
            // otherwise we need to replace the oldest item
            else
            {
                Data[Pos++] = item;
                if (Pos == Capacity) Pos = 0;
            }
        }
        /// <summary>
        /// Removes the oldest item from the list
        /// </summary>
        public T Dequeue()
        {
            --Count;
            T ret = Data[Pos++];
            if (Pos == Capacity) Pos = 0;
            return ret;
        }

        /// <summary>
        /// Clears the contents
        /// </summary>
        public void Clear()
        {
            Pos = Count = 0;
        }
    }
}
