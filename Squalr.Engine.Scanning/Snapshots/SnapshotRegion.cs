namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common;
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
        /// <param name="range">The size of this snapshot region.</param>
        public SnapshotRegion(ReadGroup readGroup, Int32 readGroupOffset, Int32 range)
        {
            this.ReadGroup = readGroup;
            this.ReadGroupOffset = readGroupOffset;
            this.Range = range;
        }

        /// <summary>
        /// Gets the readgroup from which this snapshot region reads its values.
        /// </summary>
        public ReadGroup ReadGroup { get; private set; }

        /// <summary>
        /// Gets the offset from the base of this snapshot's read group.
        /// </summary>
        public Int32 ReadGroupOffset { get; private set; }

        /// <summary>
        /// Gets the range of this snapshot region in bytes. This is the number of bytes directly contained, but more bytes may be used if tracking data types larger than 1-byte.
        /// </summary>
        public Int32 Range { get; private set; }

        /// <summary>
        /// Gets the size of this region in bytes. This requires knowing what data type is being tracked, since data types larger than 1 byte will overflow out of this region.
        /// Also, this takes into account how much space is available for reading from the underlying read group.
        /// </summary>
        public Int32 GetByteCount(Int32 dataTypeSize)
        {
            Int32 desiredSpillOverBytes = Math.Max(dataTypeSize - 1, 0);
            Int32 availableSpillOverBytes = unchecked((Int32)(this.ReadGroup.EndAddress - this.EndElementAddress));
            Int32 usedSpillOverBytes = Math.Min(desiredSpillOverBytes, availableSpillOverBytes);

            return this.Range + usedSpillOverBytes;
        }

        /// <summary>
        /// Gets the address of the first element contained in this snapshot region.
        /// </summary>
        public UInt64 BaseElementAddress
        {
            get
            {
                return unchecked(this.ReadGroup.BaseAddress + (UInt64)this.ReadGroupOffset);
            }
        }

        /// <summary>
        /// Gets the address of the last element contained in this snapshot region (assuming 1-byte alignment).
        /// </summary>
        public UInt64 EndElementAddress
        {
            get
            {
                return unchecked(this.ReadGroup.BaseAddress + (UInt64)(this.ReadGroupOffset + this.Range));
            }
        }

        /// <summary>
        /// Gets or sets the base index of this snapshot. In other words, the scan results index of the first element of this region.
        /// </summary>
        public UInt64 BaseElementIndex { get; set; }

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

            this.Range = Math.Clamp(this.Range, 0, readGroupSize - this.ReadGroupOffset - dataTypeSize);
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
        public IEnumerator<SnapshotElementIndexer> IterateElements(MemoryAlignment alignment)
        {
            Int32 elementCount = this.GetAlignedElementCount(alignment);
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