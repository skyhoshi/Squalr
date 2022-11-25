namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.Extensions;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines a region of memory in an external process.
    /// </summary>
    public class SnapshotRegion
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegion" /> class.
        /// </summary>
        /// <param name="readGroup">The read group of this snapshot region.</param>
        /// <param name="readGroupOffset">The base address of this snapshot region.</param>
        /// <param name="regionSize">The size of this snapshot region.</param>
        public SnapshotRegion(ReadGroup readGroup) : this(readGroup, 0, readGroup?.RegionSize ?? 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegion" /> class.
        /// </summary>
        /// <param name="readGroup">The read group of this snapshot region.</param>
        /// <param name="readGroupOffset">The base address of this snapshot region.</param>
        /// <param name="regionSize">The size of this snapshot region.</param>
        public SnapshotRegion(ReadGroup readGroup, Int32 readGroupOffset, Int32 regionSize)
        {
            this.ReadGroup = readGroup;
            this.ReadGroupOffset = readGroupOffset;
            this.RegionSize = regionSize;
        }

        /// <summary>
        /// Gets or sets the readgroup to which this snapshot region reads it's values.
        /// </summary>
        public ReadGroup ReadGroup { get; set; }

        /// <summary>
        /// Gets or sets the offset from the base of this snapshot's read group.
        /// </summary>
        public Int32 ReadGroupOffset { get; set; }

        /// <summary>
        /// Gets or sets the size of this snapshot region in bytes.
        /// </summary>
        public Int32 RegionSize { get; set; }

        /// <summary>
        /// Gets the base address of the region.
        /// </summary>
        public UInt64 BaseAddress
        {
            get
            {
                return this.ReadGroup.BaseAddress.Add(this.ReadGroupOffset);
            }
        }

        /// <summary>
        /// Gets the end address of the region.
        /// </summary>
        public UInt64 EndAddress
        {
            get
            {
                return this.ReadGroup.BaseAddress.Add(this.ReadGroupOffset + this.RegionSize);
            }
        }

        /// <summary>
        /// Gets or sets the base index of this snapshot. In other words, the index of the first element of this region in the scan results.
        /// </summary>
        public UInt64 BaseElementIndex { get; set; }

        /// <summary>
        /// Gets the number of elements contained in this snapshot.
        /// <param name="dataTypeSize">The size of an element.</param>
        /// <param name="alignment">The memory address alignment of each element.</param>
        /// </summary>
        public Int32 GetElementCount(Int32 dataTypeSize, MemoryAlignment alignment)
        {
            Int32 alignmentValue = unchecked((Int32)alignment);
            Int32 elementCount = this.RegionSize / 1;// (alignmentValue <= 0 ? 1 : alignmentValue);

            return elementCount;
        }

        /// <summary>
        /// Resize the snapshot region for safe reading given an allowed data type size.
        /// </summary>
        /// <param name="dataTypeSize"></param>
        public void ResizeForSafeReading(Int32 dataTypeSize)
        {
            Int32 readGroupSize = this.ReadGroup?.RegionSize ?? 0;

            this.RegionSize = Math.Clamp(this.RegionSize, 0, readGroupSize - this.ReadGroupOffset - dataTypeSize);
        }

        /// <summary>
        /// Indexer to allow the retrieval of the element at the specified index.
        /// </summary>
        /// <param name="index">The index of the snapshot element.</param>
        /// <returns>Returns the snapshot element at the specified index.</returns>
        public SnapshotElementIndexer this[Int32 index, MemoryAlignment alignment]
        {
            get
            {
                return new SnapshotElementIndexer(region: this, elementIndex: index, alignment: alignment);
            }
        }

        /// <summary>
        /// Gets the enumerator for an element reference within this snapshot region.
        /// </summary>
        /// <returns>The enumerator for an element reference within this snapshot region.</returns>
        public IEnumerator<SnapshotElementIndexer> IterateElements(Int32 elementSize, MemoryAlignment alignment)
        {
            Int32 elementCount = this.GetElementCount(elementSize, alignment);
            SnapshotElementIndexer snapshotElement = new SnapshotElementIndexer(region: this, alignment: alignment);

            for (snapshotElement.ElementIndex = 0; snapshotElement.ElementIndex < elementCount; snapshotElement.ElementIndex++)
            {
                yield return snapshotElement;
            }
        }
    }
    //// End class
}
//// End namespace