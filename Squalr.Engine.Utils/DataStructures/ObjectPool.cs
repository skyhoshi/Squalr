namespace Squalr.Engine.Common.DataStructures
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Defines a pool of objects that can be taken and returned from a shared thread-safe bag.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObjectPool<T>
    {
        /// <summary>
        /// The pool of allocated shared objects.
        /// </summary>
        private readonly ConcurrentBag<T> objectPool;

        /// <summary>
        /// The function to generate a new object if the pool is exhausted.
        /// </summary>
        private readonly Func<T> objectGenerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionScannerBase" /> class.
        /// </summary>
        /// <param name="objectGenerator">The function to generate a new object if the pool is exhausted.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ObjectPool(Func<T> objectGenerator)
        {
            this.objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            this.objectPool = new ConcurrentBag<T>();
        }

        /// <summary>
        /// Takes or creates an object from this pool.
        /// </summary>
        /// <returns>The recycled or created object.</returns>
        public T Get() => this.objectPool.TryTake(out T item) ? item : objectGenerator();

        /// <summary>
        /// Returns an object back to this pool.
        /// </summary>
        /// <param name="item">The object to return.</param>
        public void Return(T item) => this.objectPool.Add(item);

        public void Clear() => this.objectPool.Clear();
    }
    //// End class
}
//// End namespace