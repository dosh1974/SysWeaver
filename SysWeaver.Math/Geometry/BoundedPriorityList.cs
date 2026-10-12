using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SysWeaver.Geometry
{

    /// <summary>
    /// A list of limited length that remains sorted by <typeparamref name="TPriority"/>.
    /// Useful for keeping track of items in nearest neighbor searches. Insert is O(n) (binary search + list insert). Retrieval is O(1).
    /// Items are ordered by ascending priority (index 0 has the smallest priority), items with equal priorities are not ordered.
    /// </summary>
    /// <remarks>
    /// This is a struct that references two lists, copies share the same data. A default instance (not created with the constructor) can't be used.
    /// The min / max properties throw an <see cref="ArgumentOutOfRangeException"/> if the list is empty.
    /// </remarks>
    /// <typeparam name="TElement">The type of element the list maintains.</typeparam>
    /// <typeparam name="TPriority">The type the elements are prioritized by.</typeparam>
    public struct BoundedPriorityList<TElement, TPriority> : IEnumerable<TElement>
        where TPriority : IComparable<TPriority>
    {
        /// <summary>
        /// The list holding the actual elements
        /// </summary>
        private readonly List<TElement> elementList;

        /// <summary>
        /// The list of priorities for each element.
        /// There is a one-to-one correspondence between the
        /// priority list ad the element list.
        /// </summary>
        private readonly List<TPriority> priorityList;

        /// <summary>
        /// Gets the element with the largest priority.
        /// </summary>
        public TElement MaxElement => this.elementList[this.elementList.Count - 1];

        /// <summary>
        /// Gets the largest priority.
        /// </summary>
        public TPriority MaxPriority => this.priorityList[this.priorityList.Count - 1];

        /// <summary>
        /// Gets the element with the lowest priority.
        /// </summary>
        public TElement MinElement => this.elementList[0];

        /// <summary>
        /// Gets the smallest priority.
        /// </summary>
        public TPriority MinPriority => this.priorityList[0];

        /// <summary>
        /// Gets the maximum allows capacity for the <see cref="BoundedPriorityList{TElement,TPriority}"/>
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// Returns true if the list is at maximum capacity.
        /// </summary>
        public bool IsFull => this.Count == this.Capacity;

        /// <summary>
        /// Returns the count of items currently in the list.
        /// </summary>
        public int Count => this.priorityList.Count;

        /// <summary>
        /// Indexer for the internal element array.
        /// </summary>
        /// <param name="index">The index in the array.</param>
        /// <returns>The element at the specified index.</returns>
        public TElement this[int index] => this.elementList[index];

        /// <summary>
        /// Initializes a new instance of the <see cref="BoundedPriorityList{TElement, TPriority}"/> class.
        /// Note: You should not have <paramref name="allocate"/> set to true, and the capacity set to a very large number.
        /// Especially if you will be creating and destroying many <see cref="BoundedPriorityList{TElement,TPriority}"/> very rapidly.
        /// If you ignore this advice you will create lots of memory pressure. If you don't understand why this is a problem you should
        /// understand the garbage collector. Please read: https://msdn.microsoft.com/en-us/library/ee787088.aspx
        /// </summary>
        /// <param name="capacity">The maximum capacity of the list.</param>
        /// <param name="allocate">If true, initializes the internal lists for the <see cref="BoundedPriorityList{TElement,TPriority}"/> with an initial capacity of <paramref name="capacity"/>.</param>
        public BoundedPriorityList(int capacity, bool allocate = false)
        {
            this.Capacity = capacity;
            if (allocate)
            {
                this.priorityList = new List<TPriority>(capacity);
                this.elementList = new List<TElement>(capacity);
            }
            else
            {
                this.priorityList = new List<TPriority>();
                this.elementList = new List<TElement>();
            }
        }

        /// <summary>
        /// Attempts to add the provided  <paramref name="item"/>. If the list
        /// is currently at maximum capacity and the elements priority is greater
        /// than the highest priority, the <paramref name = "item"/> is not inserted. If the
        /// <paramref name = "item"/> is eligible for insertion, the upon insertion the <paramref name = "item"/> that previously
        /// had the largest priority is removed from the list (so an item with the same priority as the largest priority replaces an item with that priority).
        /// Nothing is added if the capacity is zero or negative.
        /// The position is found with a binary search (O(log n)), the insertion itself is O(n).
        /// </summary>
        /// <param name="item">The item to be inserted</param>
        /// <param name="priority">The priority of th given item.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TElement item, TPriority priority)
        {
            if (this.Count >= this.Capacity)
            {
                if (this.Count <= 0)
                    return;
                if (this.priorityList[this.priorityList.Count - 1].CompareTo(priority) < 0)
                {
                    return;
                }

                var index = this.priorityList.BinarySearch(priority);
                index = index >= 0 ? index : ~index;

                //  Remove the last item before inserting (the lists never grow beyond the capacity, and one less item is moved),
                //  inserting at the end and removing the last item would leave the list unchanged
                var last = this.priorityList.Count - 1;
                if (index > last)
                    return;
                this.priorityList.RemoveAt(last);
                this.elementList.RemoveAt(last);

                this.priorityList.Insert(index, priority);
                this.elementList.Insert(index, item);
            }
            else
            {
                var index = this.priorityList.BinarySearch(priority);
                index = index >= 0 ? index : ~index;

                this.priorityList.Insert(index, priority);
                this.elementList.Insert(index, item);
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator.</returns>
        public IEnumerator<TElement> GetEnumerator()
        {
            return this.elementList.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
