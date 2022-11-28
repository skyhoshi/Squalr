namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.Extensions;
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
        /// <param name="elementCount">The size of this snapshot region.</param>
        public SnapshotRegion(ReadGroup readGroup, Int32 readGroupOffset, Int32 elementCount)
        {
            this.ReadGroup = readGroup;
            this.ReadGroupOffset = readGroupOffset;
            this.ElementCount = elementCount;
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
        /// Gets the size of this snapshot region in elements. This is the number of bytes contained, but if accessing data types larger than 
        /// </summary>
        public Int32 ElementCount { get; private set; }

        /// <summary>
        /// Gets the size of this region in bytes. Note that doing such requires knowing what data type is being tracked, since data types larger than 1 byte will overflow out of this region.
        /// </summary>
        public Int32 GetByteCount(Int32 dataTypeSize)
        {
            return this.ElementCount + Math.Clamp(dataTypeSize, 1, dataTypeSize) - 1;
        }

        /// <summary>
        /// Gets the address of the first element contained in this snapshot region.
        /// </summary>
        public UInt64 BaseElementAddress
        {
            get
            {
                return this.ReadGroup.BaseAddress.Add(this.ReadGroupOffset);
            }
        }

        /// <summary>
        /// Gets the address of the last element contained in this snapshot region.
        /// </summary>
        public UInt64 EndElementAddress
        {
            get
            {
                return this.ReadGroup.BaseAddress.Add(this.ReadGroupOffset + this.ElementCount, wrapAround: false);
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
            Int32 elementCount = this.ElementCount / (alignmentValue <= 0 ? 1 : alignmentValue);

            return elementCount;
        }

        /// <summary>
        /// Resize the snapshot region for safe reading given an allowed data type size.
        /// </summary>
        /// <param name="dataTypeSize"></param>
        public void ResizeForSafeReading(Int32 dataTypeSize)
        {
            Int32 readGroupSize = this.ReadGroup?.RegionSize ?? 0;

            this.ElementCount = Math.Clamp(this.ElementCount, 0, readGroupSize - this.ReadGroupOffset - dataTypeSize);
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