namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.DataStructures;
    using Squalr.Engine.Common.Extensions;
    using Squalr.Engine.Common.Hardware;
    using Squalr.Engine.Memory;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Linq;

    /// <summary>
    /// Defines a segment of process memory, which many snapshot sub regions may read from. This serves as a shared pool of memory, such as to
    /// minimize the number of calls to the OS to read the memory of a process.
    /// </summary>
    public class SnapshotRegion : NormalizedRegion, IEnumerable<SnapshotElementRange>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegion" /> class.
        /// </summary>
        /// <param name="baseAddress">The base address of this memory region.</param>
        /// <param name="regionSize">The size of this memory region.</param>
        public SnapshotRegion(UInt64 baseAddress, Int32 regionSize) : base(baseAddress, regionSize)
        {
            // Create one large snapshot element range spanning the entire region by default
            this.SnapshotElementRanges = new List<SnapshotElementRange>() { new SnapshotElementRange(this) };
            this.SnapshotElementRangeIndexLookupTable = new IntervalTree<UInt64, SnapshotElementRange>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegion" /> class.
        /// </summary>
        /// <param name="baseAddress">The base address of this memory region.</param>
        /// <param name="regionSize">The size of this memory region.</param>
        public SnapshotRegion(UInt64 baseAddress, Byte[] initialBytes) : this(baseAddress, initialBytes.Length)
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
        /// Get or set the snapshot element ranges in this snapshot. These are elements discovered by scans.
        /// </summary>
        public IEnumerable<SnapshotElementRange> SnapshotElementRanges { get; set; }

        /// <summary>
        /// Gets or sets the element index for this snapshot regions in the scan results.
        /// </summary>
        public UInt64 BaseElementIndex { get; set; }

        /// <summary>
        /// Gets the most recently computed number of bytes contained in this snapshot region.
        /// </summary>
        public Int32 ElementByteCount { get; private set; }

        /// <summary>
        /// Gets the most recently computed number of elements contained in this snapshot region.
        /// </summary>
        public Int32 AlignedElementCount { get; private set; }

        /// <summary>
        /// Gets or sets a lookup table used for querying scan results quickly.
        /// </summary>
        private IntervalTree<UInt64, SnapshotElementRange> SnapshotElementRangeIndexLookupTable { get; set; }

        /// <summary>
        /// Indexer to allow the retrieval of the element at the specified index. This does NOT index into a region.
        /// </summary>
        /// <param name="elementIndex">The index of the snapshot element.</param>
        /// <returns>Returns the snapshot element at the specified index.</returns>
        public SnapshotElementIndexer this[UInt64 elementIndex, MemoryAlignment alignment]
        {
            get
            {
                SnapshotElementRange elementRange = this.SnapshotElementRangeIndexLookupTable.QueryOne(elementIndex);

                if (elementRange == null)
                {
                    return null;
                }

                Int32 elementRangeIndex = (elementIndex - elementRange.BaseElementIndex).ToInt32();

                SnapshotElementIndexer indexer = new SnapshotElementIndexer(elementRange, MemoryAlignment.Alignment1, elementRangeIndex);

                return indexer;
            }
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
        /// Gets the size in bytes of all elements contained in this snapshot region, based on the provided element data type size.
        /// </summary>
        /// <param name="dataTypeSize">The data type size of the elements contained by element ranges in this function.</param>
        public void ComputeByteAndElementCounts(Int32 dataTypeSize, MemoryAlignment alignment)
        {
            this.ElementByteCount = 0;
            this.AlignedElementCount = 0;
            this.SnapshotElementRangeIndexLookupTable.Clear();

            if (this.SnapshotElementRanges != null)
            {
                foreach (SnapshotElementRange elementRange in this.SnapshotElementRanges)
                {
                    this.ElementByteCount += elementRange.GetByteCount(dataTypeSize);
                    this.AlignedElementCount += elementRange.GetAlignedElementCount(alignment);
                    this.SnapshotElementRangeIndexLookupTable.Add(this.BaseElementIndex, this.BaseElementIndex + (UInt64)this.AlignedElementCount, elementRange);
                }
            }
        }

        /// <summary>
        /// Determines if a relative comparison can be done for this region, ie current and previous values are loaded.
        /// </summary>
        /// <param name="constraints">The collection of scan constraints to use in the manual scan.</param>
        /// <returns>True if a relative comparison can be done for this region.</returns>
        public Boolean CanCompare(IScanConstraint constraints)
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
        /// Gets an enumerator for all snapshot element ranges within this snapshot region.
        /// </summary>
        /// <returns>An enumerator for all snapshot element ranges within this snapshot region.</returns>
        public IEnumerator<SnapshotElementRange> GetEnumerator()
        {
            return SnapshotElementRanges?.AsEnumerable()?.GetEnumerator();
        }

        /// <summary>
        /// Gets an enumerator for all snapshot element ranges within this snapshot region.
        /// </summary>
        /// <returns>An enumerator for all snapshot element ranges within this snapshot region.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return SnapshotElementRanges?.GetEnumerator();
        }
    }
    //// End class
}
//// End namespace