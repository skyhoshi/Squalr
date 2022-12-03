namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines a range of snapshot elements in an external process.
    /// </summary>
    public class SnapshotElementRange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotElementRange" /> class.
        /// </summary>
        /// <param name="readGroup">The read group of this snapshot region.</param>
        /// <param name="readGroupOffset">The base address of this snapshot region.</param>
        /// <param name="regionSize">The size of this snapshot region.</param>
        public SnapshotElementRange(SnapshotRegion readGroup) : this(readGroup, 0, readGroup?.RegionSize ?? 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotElementRange" /> class.
        /// </summary>
        /// <param name="readGroup">The read group of this snapshot region.</param>
        /// <param name="readGroupOffset">The base address of this snapshot region.</param>
        /// <param name="range">The size of this snapshot region.</param>
        public SnapshotElementRange(SnapshotRegion readGroup, Int32 readGroupOffset, Int32 range)
        {
            this.ReadGroup = readGroup;
            this.RegionOffset = readGroupOffset;
            this.Range = range;
        }

        /// <summary>
        /// Gets the snapshot region from which this element range reads its values.
        /// </summary>
        public SnapshotRegion ReadGroup { get; private set; }

        /// <summary>
        /// Gets the offset from the base of the snapshot region that contains this element range.
        /// </summary>
        public Int32 RegionOffset { get; private set; }

        /// <summary>
        /// Gets the size of this element range in bytes. This is the number of bytes directly contained, but more bytes may be used if tracking data types larger than 1-byte.
        /// </summary>
        public Int32 Range { get; private set; }

        /// <summary>
        /// Gets the address of the first element contained in this snapshot region.
        /// </summary>
        public UInt64 BaseElementAddress
        {
            get
            {
                return unchecked(this.ReadGroup.BaseAddress + (UInt64)this.RegionOffset);
            }
        }

        /// <summary>
        /// Gets the address of the last element contained in this snapshot region (assuming 1-byte alignment).
        /// </summary>
        public UInt64 EndElementAddress
        {
            get
            {
                return unchecked(this.ReadGroup.BaseAddress + (UInt64)(this.RegionOffset + this.Range));
            }
        }

        /// <summary>
        /// Gets or sets the base index of this snapshot. In other words, the scan results index of the first element of this region.
        /// </summary>
        public UInt64 BaseElementIndex { get; set; }

        /// <summary>
        /// Gets the size of this range in bytes. This requires knowing what data type is being tracked, since data types larger than 1 byte will overflow out of this region.
        /// Also, this takes into account how much space is available for reading from the snapshot region containing this element range.
        /// </summary>
        /// <param name="dataTypeSize">The data type size of the elements contained.</param>
        /// <returns></returns>
        public Int32 GetByteCount(Int32 dataTypeSize)
        {
            Int32 desiredSpillOverBytes = Math.Max(dataTypeSize - 1, 0);
            Int32 availableSpillOverBytes = unchecked((Int32)(this.ReadGroup.EndAddress - this.EndElementAddress));
            Int32 usedSpillOverBytes = Math.Min(desiredSpillOverBytes, availableSpillOverBytes);

            return this.Range + usedSpillOverBytes;
        }

        /// <summary>
        /// Gets the number of elements contained in this snapshot.
        /// <param name="alignment">The memory address alignment of each element.</param>
        /// </summary>
        public Int32 GetAlignedElementCount(MemoryAlignment alignment)
        {
            Int32 alignmentValue = unchecked((Int32)alignment);
            Int32 elementCount = this.Range / (alignmentValue <= 0 ? 1 : alignmentValue);

            return elementCount;
        }

        /// <summary>
        /// Resize the snapshot region for safe reading given an allowed data type size.
        /// </summary>
        /// <param name="dataTypeSize"></param>
        public void ResizeForSafeReading(Int32 dataTypeSize)
        {
            Int32 readGroupSize = this.ReadGroup?.RegionSize ?? 0;

            this.Range = Math.Clamp(this.Range, 0, readGroupSize - this.RegionOffset - dataTypeSize);
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
                return new SnapshotElementIndexer(elementRange: this, elementIndex: index, alignment: alignment);
            }
        }

        /// <summary>
        /// Gets the enumerator for an element reference within this snapshot region.
        /// </summary>
        /// <returns>The enumerator for an element reference within this snapshot region.</returns>
        public IEnumerator<SnapshotElementIndexer> IterateElements(MemoryAlignment alignment)
        {
            Int32 elementCount = this.GetAlignedElementCount(alignment);
            SnapshotElementIndexer snapshotElement = new SnapshotElementIndexer(elementRange: this, alignment: alignment);

            for (snapshotElement.ElementIndex = 0; snapshotElement.ElementIndex < elementCount; snapshotElement.ElementIndex++)
            {
                yield return snapshotElement;
            }
        }
    }
    //// End class
}
//// End namespace