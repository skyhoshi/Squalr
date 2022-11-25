namespace Squalr.Engine.Scanning.Scanners.Comparers
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A class for producing snapshot regions from a scanned snapshot region via run length encoded scan matches.
    /// This is one of the magic tricks Squalr uses for fast scans. Each scan thread uses run length encoding to track the number of consecutive
    /// successful scan matches. Once a non-matching element is found, a snapshot region is created containing the contiguous block of successful results.
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
            this.DataTypeSize = constraints.ElementType.Size;
            this.ResultRegions = new List<SnapshotRegion>();
            this.RunLengthEncodeOffset = region?.ReadGroupOffset ?? 0;

            if (constraints.ElementType is ByteArrayType)
            {
                this.Alignment = MemoryAlignment.Alignment1;
            }
            else
            {
                this.Alignment = constraints.Alignment == MemoryAlignment.Auto ? (MemoryAlignment)this.DataTypeSize : constraints.Alignment;
            }
        }

        /// <summary>
        /// Gets or sets the current base address offset from which the run length encoding has started.
        /// </summary>
        private Int32 RunLengthEncodeOffset { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether we are currently encoding a new result region.
        /// </summary>
        private Boolean IsEncoding { get; set; }

        /// <summary>
        /// Gets or sets the current run length for run length encoded current scan results.
        /// </summary>
        private Int32 RunLength { get; set; }

        /// <summary>
        /// Gets or sets the size of the data type being compared.
        /// </summary>
        private Int32 DataTypeSize { get; set; }

        /// <summary>
        /// Gets or sets the parent snapshot region.
        /// </summary>
        private SnapshotRegion Region { get; set; }

        /// <summary>
        /// Gets or sets the list of discovered result regions.
        /// </summary>
        private IList<SnapshotRegion> ResultRegions { get; set; }

        /// <summary>
        /// Gets or sets the enforced memory alignment for this scan.
        /// </summary>
        private MemoryAlignment Alignment { get; set; }

        /// <summary>
        /// Finalizes any leftover snapshot regions and returns them.
        /// </summary>
        public IList<SnapshotRegion> GatherCollectedRegions()
        {
            this.FinalizeCurrentEncode();
            return this.ResultRegions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EncodeBatch(Int32 advanceByteCount)
        {
            this.RunLength += advanceByteCount;
            this.IsEncoding = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EncodeOne()
        {
            this.RunLength++;
            this.IsEncoding = true;
        }

        /// <summary>
        /// Encodes the current scan results if possible. This finalizes the current run-length encoded scan results to a snapshot region.
        /// </summary>
        /// <param name="advanceByteCount">The number of failed bytes (ie values that did not match scans) to increment by.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FinalizeCurrentEncode(Int32 advanceByteCount = 0)
        {
            // Create the final region if we are still encoding
            if (this.IsEncoding)
            {
                // Run length is in bytes, but snapshot regions need to know total number of elements, which depends on the data type and alignment
                UInt64 absoluteAddressStart = this.Region.ReadGroup.BaseAddress + (UInt64)this.RunLengthEncodeOffset;
                UInt64 absoluteAddressEnd = absoluteAddressStart + (UInt64)this.RunLength;

                // Vector comparisons can produce some false positives since vectors can load values outside of the original snapshot range. This can result in next scans actually increasing the result count.
                // This is particularly true in "next scans". This check catches any potential errors introduced this way.
                // TODO: This is really bad and impacts performance. The vector scanner should handle this, rather than pushing this bug here.
                if (absoluteAddressStart >= this.Region.BaseAddress && absoluteAddressEnd <= this.Region.EndAddress)
                {
                    this.ResultRegions.Add(new SnapshotRegion(this.Region.ReadGroup, this.RunLengthEncodeOffset, this.RunLength));
                }

                this.RunLengthEncodeOffset += this.RunLength;
                this.RunLength = 0;
                this.IsEncoding = false;
            }

            this.RunLengthEncodeOffset += advanceByteCount;
        }
    }
    //// End class
}
//// End namespace
