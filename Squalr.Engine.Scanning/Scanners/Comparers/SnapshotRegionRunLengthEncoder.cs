namespace Squalr.Engine.Scanning.Scanners.Comparers
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A structure for producing snapshot regions from a scanned snapshot region via run length encoded scan matches.
    /// </summary>
    internal unsafe class SnapshotRegionRunLengthEncoder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionRunLengthEncoder" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The set of constraints to use for the element comparisons.</param>
        public SnapshotRegionRunLengthEncoder(SnapshotRegion region, ScanConstraints constraints)
        {
            this.Region = region;
            this.DataType = constraints.ElementType;
            this.DataTypeSize = constraints.ElementType.Size;
            this.ResultRegions = new List<SnapshotRegion>();

            if (this.DataType is ByteArrayType)
            {
                this.Alignment = MemoryAlignment.Alignment1;
            }
            else
            {
                this.Alignment = constraints.Alignment == MemoryAlignment.Auto ? (MemoryAlignment)this.DataTypeSize : constraints.Alignment;
            }
        }

        /// <summary>
        /// Gets or sets the index from which the run length encoding is started.
        /// </summary>
        public Int32 RunLengthEncodeOffset { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether we are currently encoding a new result region.
        /// </summary>
        public Boolean IsEncoding { get; set; }

        /// <summary>
        /// Gets or sets the current run length for run length encoded current scan results.
        /// </summary>
        public Int32 RunLength { get; set; }

        /// <summary>
        /// Gets or sets the size of the data type being compared.
        /// </summary>
        protected Int32 DataTypeSize { get; set; }

        /// <summary>
        /// Gets or sets the data type being compared.
        /// </summary>
        protected ScannableType DataType { get; set; }

        /// <summary>
        /// Gets or sets the parent snapshot region.
        /// </summary>
        protected SnapshotRegion Region { get; set; }

        /// <summary>
        /// Gets or sets the list of discovered result regions.
        /// </summary>
        protected IList<SnapshotRegion> ResultRegions { get; set; }

        /// <summary>
        /// Gets or sets the enforced memory alignment for this scan.
        /// </summary>
        protected MemoryAlignment Alignment { get; set; }

        /// <summary>
        /// Finalizes any leftover snapshot regions and returns them.
        /// </summary>
        public IList<SnapshotRegion> GatherCollectedRegions(Int32 readGroupBase)
        {
            this.EncodeCurrentResults(readGroupBase);
            return this.ResultRegions;
        }

        /// <summary>
        /// Encodes the current scan results if possible. This finalizes the current run-length encoded scan results to a snapshot region.
        /// </summary>
        /// <param name="readGroupOffset">The base address of the read group.</param>
        /// <param name="readGroupOffset">The offset into the read group.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EncodeCurrentResults(Int32 readGroupBase, Int32 readGroupOffset = 0)
        {
            // Create the final region if we are still encoding
            if (this.IsEncoding)
            {
                Int32 finalReadGroupOffset = readGroupBase + readGroupOffset - this.RunLength * unchecked((Int32)this.Alignment);
                UInt64 absoluteAddressStart = this.Region.ReadGroup.BaseAddress + unchecked((UInt64)finalReadGroupOffset);
                UInt64 absoluteAddressEnd = absoluteAddressStart + unchecked((UInt64)this.RunLength);

                // Vector comparisons can produce some false positives since vectors can load values outside of the snapshot range.
                // This check catches any potential errors introduced this way.
                if (absoluteAddressStart >= this.Region.BaseAddress && absoluteAddressEnd <= this.Region.EndAddress)
                {
                    this.ResultRegions.Add(new SnapshotRegion(this.Region.ReadGroup, finalReadGroupOffset, this.RunLength));
                }

                this.RunLength = 0;
                this.IsEncoding = false;
            }
        }
    }
    //// End class
}
//// End namespace
