namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.OS;
    using Squalr.Engine.Memory;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Defines a segment of process memory, which many snapshot regions may read from. This serves as a shared pool of memory, such as to
    /// minimize the number of calls to the OS to read the memory of a process.
    /// </summary>
    public class ReadGroup : NormalizedRegion
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReadGroup" /> class.
        /// </summary>
        /// <param name="baseAddress">The base address of this memory region.</param>
        /// <param name="regionSize">The size of this memory region.</param>
        public ReadGroup(UInt64 baseAddress, Int32 regionSize) : base(baseAddress, regionSize)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadGroup" /> class.
        /// </summary>
        /// <param name="baseAddress">The base address of this memory region.</param>
        /// <param name="regionSize">The size of this memory region.</param>
        public ReadGroup(UInt64 baseAddress, Byte[] initialBytes) : base(baseAddress, initialBytes.Length)
        {
            this.CurrentValues = initialBytes;
        }

        /// <summary>
        /// Gets the most recently read values.
        /// </summary>
        public unsafe Byte[] CurrentValues { get; private set; }

        /// <summary>
        /// Gets the previously read values.
        /// </summary>
        public unsafe Byte[] PreviousValues { get; private set; }

        /// <summary>
        /// Gets the element labels.
        /// </summary>
        public unsafe Object[] ElementLabels { get; private set; }

        /// <summary>
        /// Gets or sets the data type of the labels of this region.
        /// </summary>
        public ScannableType LabelDataType { get; set; }

        public IEnumerable<SnapshotRegion> Shard(Int32 shardSize)
        {
            IList<SnapshotRegion> regions = new List<SnapshotRegion>();

            shardSize = Math.Min(shardSize / Vectors.VectorSize * Vectors.VectorSize, this.RegionSize);

            Int32 remaining = this.RegionSize;
            Int32 offset = 0;

            while (remaining > 0)
            {
                regions.Add(new SnapshotRegion(this, offset, Math.Min(shardSize, remaining)));
                offset += shardSize;
                remaining -= shardSize;
            }

            return regions;
        }

        /// <summary>
        /// Reads all memory for this memory region.
        /// </summary>
        /// <returns>The bytes read from memory.</returns>
        public unsafe Boolean ReadAllMemory(Process process)
        {
            this.SetPreviousValues(this.CurrentValues);
            this.SetCurrentValues(MemoryReader.Instance.ReadBytes(process, this.BaseAddress, this.RegionSize, out bool readSuccess));

            if (!readSuccess)
            {
                this.SetPreviousValues(null);
                this.SetCurrentValues(null);
            }

            return readSuccess;
        }

        /// <summary>
        /// Determines if a relative comparison can be done for this region, ie current and previous values are loaded.
        /// </summary>
        /// <param name="constraints">The collection of scan constraints to use in the manual scan.</param>
        /// <returns>True if a relative comparison can be done for this region.</returns>
        public Boolean CanCompare(Constraint constraints)
        {
            if (constraints == null
                || !constraints.IsValid()
                || this.CurrentValues == null
                || ((constraints as ScanConstraint)?.IsRelativeConstraint() ?? false) && this.PreviousValues == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sets the current values of this region.
        /// </summary>
        /// <param name="newValues">The raw bytes of the values.</param>
        public void SetCurrentValues(Byte[] newValues)
        {
            this.CurrentValues = newValues;
        }

        /// <summary>
        /// Sets the previous values of this region.
        /// </summary>
        /// <param name="newValues">The raw bytes of the values.</param>
        public void SetPreviousValues(Byte[] newValues)
        {
            this.PreviousValues = newValues;
        }

        /// <summary>
        /// Sets the element labels for this snapshot region.
        /// </summary>
        /// <param name="newLabels">The new labels to be assigned.</param>
        public void SetElementLabels(Object[] newLabels)
        {
            this.ElementLabels = newLabels;
        }
    }
    //// End class
}
//// End namespace