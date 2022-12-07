namespace Squalr.Engine.Common.DataStructures
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public class BinaryHeap<T> : IEnumerable<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryHeap{T}" /> class.
        /// </summary>
        public BinaryHeap() : this(Comparer<T>.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryHeap{T}" /> class.
        /// </summary>
        /// <param name="comparer">The custom comparer to use for the binary heap.</param>
        public BinaryHeap(IComparer<T> comparer)
        {
            this.Comparer = comparer;
            this.Items = new List<T>();
        }

        public Int32 Count
        {
            get
            {
                return this.Items.Count;
            }
        }

        protected IComparer<T> Comparer { get; set; }

        /// <summary>
        /// Gets or sets a list that holds all the items in the heap.
        /// </summary>
        protected List<T> Items { get; set; }

        public virtual void Insert(T newItem)
        {
            Int32 index = this.Count;

            // Add the new item to the bottom of the heap.
            this.Items.Add(newItem);

            // Until the new item is greater than its parent item, swap the two
            while (index > 0 && this.Comparer.Compare(this.Items[(index - 1) / 2], newItem) > 0)
            {
                this.Items[index] = this.Items[(index - 1) / 2];

                index = (index - 1) / 2;
            }

            // The new index in the list is the appropriate location for the new item
            this.Items[index] = newItem;
        }

        public T Last()
        {
            return this.Items[this.Items.Count - 1];
        }

        public T[] ToArray()
        {
            return this.Items.ToArray();
        }

        public void Clear()
        {
            this.Items.Clear();
        }

        public virtual IEnumerator GetEnumerator()
        {
            return this.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            foreach (T element in this.Items)
            {
                yield return element;
            }
        }
    }
    //// End class
}
//// End namespace