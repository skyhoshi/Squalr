namespace Squalr.Engine.Common.DataStructures
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public class IntervalTree<TKey, TValue> : IIntervalTree<TKey, TValue>
    {
        /// <summary>
        /// Initializes an empty tree.
        /// </summary>
        public IntervalTree() : this(Comparer<TKey>.Default)
        {
        }

        /// <summary>
        /// Initializes an empty tree.
        /// </summary>
        public IntervalTree(IComparer<TKey> comparer)
        {
            this.Comparer = comparer ?? Comparer<TKey>.Default;
            this.IsInSync = true;
            this.Root = new IntervalTreeNode<TKey, TValue>(this.Comparer);
            this.Items = new List<RangeValuePair<TKey, TValue>>();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public TKey Max
        {
            get
            {
                if (!this.IsInSync)
                {
                    this.Rebuild();
                }

                return this.Root.Max;
            }
        }

        public TKey Min
        {
            get
            {
                if (!this.IsInSync)
                {
                    this.Rebuild();
                }

                return this.Root.Min;
            }
        }

        public IEnumerable<TValue> Values => this.Items.Select(i => i.Value);

        public Int32 Count => Items.Count;

        private IntervalTreeNode<TKey, TValue> Root { get; set; }

        private List<RangeValuePair<TKey, TValue>> Items { get; set; }

        private IComparer<TKey> Comparer { get; set; }

        private Boolean IsInSync { get; set; }

        /// <summary>
        /// Performs a point query with a single value. The first match is returned.
        /// </summary>
        public TValue QueryOne(TKey value)
        {
            if (!this.IsInSync)
            {
                Rebuild();
            }

            return this.Root.QueryOne(value);
        }

        public IEnumerable<TValue> Query(TKey value)
        {
            if (!this.IsInSync)
            {
                Rebuild();
            }

            return this.Root.Query(value);
        }

        public IEnumerable<TValue> Query(TKey from, TKey to)
        {
            if (!this.IsInSync)
            {
                Rebuild();
            }

            return this.Root.Query(from, to);
        }

        public void Add(TKey from, TKey to, TValue value)
        {
            if (this.Comparer.Compare(from, to) > 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(from)} cannot be larger than {nameof(to)}");
            }

            this.IsInSync = false;
            this.Items.Add(new RangeValuePair<TKey, TValue>(from, to, value));
        }

        public void Remove(TValue value)
        {
            this.IsInSync = false;
            this.Items = this.Items.Where(item => !item.Value.Equals(value)).ToList();
        }

        public void Remove(IEnumerable<TValue> items)
        {
            this.IsInSync = false;
            this.Items = this.Items.Where(item => !items.Contains(item.Value)).ToList();
        }

        public void Clear()
        {
            this.Root = new IntervalTreeNode<TKey, TValue>(this.Comparer);
            this.Items = new List<RangeValuePair<TKey, TValue>>();
            this.IsInSync = true;
        }

        public IEnumerator<RangeValuePair<TKey, TValue>> GetEnumerator()
        {
            if (!this.IsInSync)
            {
                this.Rebuild();
            }

            return this.Items.GetEnumerator();
        }

        private void Rebuild()
        {
            if (this.IsInSync)
            {
                return;
            }

            if (this.Items.Count > 0)
            {
                this.Root = new IntervalTreeNode<TKey, TValue>(this.Items, this.Comparer);
            }
            else
            {
                this.Root = new IntervalTreeNode<TKey, TValue>(this.Comparer);
            }

            this.IsInSync = true;
        }
    }
    //// End class
}
//// End namespace