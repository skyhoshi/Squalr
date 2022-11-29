namespace Squalr.Engine.Scanning.Scanners.Comparers
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.OS;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Drawing;

    /// <summary>
    /// A faster version of SnapshotElementComparer that takes advantage of vectorization/SSE instructions.
    /// </summary>
    internal unsafe abstract class SnapshotRegionScannerBase : ISnapshotRegionScanner
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionScannerBase" /> class.
        /// </summary>
        public SnapshotRegionScannerBase()
        {
            this.RunLengthEncoder = new SnapshotRegionRunLengthEncoder();
        }

        /// <summary>
        /// Gets or sets the index from which the next vector is read.
        /// </summary>
        public Int32 VectorReadOffset { get; protected set; }

        /// <summary>
        /// Gets or sets the alignment offset, which is also used for reading the next vector.
        /// </summary>
        public Int32 AlignmentReadOffset { get; protected set; }

        /// <summary>
        /// Gets the current values at the current vector read index.
        /// </summary>
        public UInt64 CurrentAddress
        {
            get
            {
                return Region.ReadGroup.BaseAddress + unchecked((UInt32)(this.VectorReadBase + this.VectorReadOffset + this.AlignmentReadOffset));
            }
        }

        /// <summary>
        /// Iterator for array of bytes vectorized chunks.
        /// </summary>
        protected Int32 ArrayOfBytesChunkIndex { get; set; }

        /// <summary>
        /// Gets or sets the parent snapshot region.
        /// </summary>
        protected SnapshotRegion Region { get; set; }

        /// <summary>
        /// Gets or sets a snapshot region run length encoder, which is used to create the snapshots from this scan.
        /// </summary>
        protected SnapshotRegionRunLengthEncoder RunLengthEncoder { get; private set; }

        /// <summary>
        /// Gets or sets the index of this element.
        /// </summary>
        protected Int32 VectorReadBase { get; set; }

        /// <summary>
        /// Gets or sets the SSE vector size on the machine.
        /// </summary>
        protected Int32 VectorSize { get; set; }

        /// <summary>
        /// Gets or sets the size of the data type being compared.
        /// </summary>
        protected Int32 DataTypeSize { get; set; }

        /// <summary>
        /// Gets or sets the enforced memory alignment for this scan.
        /// </summary>
        protected MemoryAlignment Alignment { get; set; }

        /// <summary>
        /// Gets or sets the data type being compared.
        /// </summary>
        protected ScannableType DataType { get; set; }

        /// <summary>
        /// The action to perform when disposing this scanner.
        /// </summary>
        private Action OnDispose { get; set; }

        /// <summary>
        /// Initializes this scanner for the given region and constaints.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The set of constraints to use for the element comparisons.</param>
        public virtual void Initialize(SnapshotRegion region, ScanConstraints constraints)
        {
            this.RunLengthEncoder.Initialize(region);
            this.Region = region;
            this.VectorSize = Vectors.VectorSize;
            this.VectorReadBase = this.Region.ReadGroupOffset;
            this.VectorReadOffset = 0;
            this.DataType = constraints.ElementType;
            this.DataTypeSize = constraints.ElementType.Size;

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
        /// Sets the action to perform when the scanner is disposed. Note that a disposed scanner is not necessarily destroyed, as these objects may be recycled for future scans.
        /// </summary>
        /// <param name="onDispose">The dispose function callback.</param>
        public void SetDisposeCallback(Action onDispose)
        {
            this.OnDispose = onDispose;
        }

        /// <summary>
        /// Perform cleanup and release references, since this snapshot scanner instance may be recycled and exist in cached memory.
        /// </summary>
        public virtual void Dispose()
        {
            this.RunLengthEncoder.Dispose();
            this.Region = null;

            if (this.OnDispose != null)
            {
                this.OnDispose();
            }
        }

        /// <summary>
        /// Performs a scan over the given region, returning the discovered regions.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public abstract IList<SnapshotRegion> ScanRegion(SnapshotRegion region, ScanConstraints constraints);
    }
    //// End class
}
//// End namespace
