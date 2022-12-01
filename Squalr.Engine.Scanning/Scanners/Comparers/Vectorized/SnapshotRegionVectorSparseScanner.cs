namespace Squalr.Engine.Scanning.Scanners.Comparers.Vectorized
{
    using Squalr.Engine.Common.OS;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A vector scanner implementation that can handle sparse (alignment greater than data type size) vector scans.
    /// </summary>
    internal unsafe class SnapshotRegionVectorSparseScanner : SnapshotRegionVectorScannerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionVectorFastScanner" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The set of constraints to use for the element comparisons.</param>
        public SnapshotRegionVectorSparseScanner() : base()
        {
        }

        /// <summary>
        /// Performs a scan over the given region, returning the discovered regions.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public override IList<SnapshotRegion> ScanRegion(SnapshotRegion region, ScanConstraints constraints)
        {
            this.Initialize(region: region, constraints: constraints);

            // This algorithm is mostly the same as SnapshotRegionVectorFastScanner, but with the addition of a sparse mask.
            // This mask automatically captures all in-between elements. For example, scanning for Byte 0 against <0, 24, 0, 43> with an alignment of 2-bytes would all return true, due to this mask of <0, 255, 0, 255>.
            // This is good, because the scan results will automatically skip over the unwanted elements. We do NOT want to break this into two separate snapshot regions, since this would be incredibly inefficient.

            Int32 scanCount = this.Region.Range / Vectors.VectorSize + (this.VectorOverread > 0 ? 1 : 0);
            Vector<Byte> misalignmentMask = this.BuildVectorMisalignmentMask();
            Vector<Byte> overreadMask = this.BuildVectorOverreadMask();
            Vector<Byte> sparseMask = this.BuildSparseMask();

            // Perform the first scan (there should always be at least one). Apply the misalignment mask, and optionally the overread mask if this is also the finals scan.
            Vector<Byte> scanResults = Vector.BitwiseAnd(Vector.BitwiseAnd(misalignmentMask, Vector.BitwiseOr(this.VectorCompare(), sparseMask)), scanCount == 1 ? overreadMask : Vector<Byte>.One);
            this.EncodeScanResults(ref scanResults, ref sparseMask);
            this.VectorReadOffset += Vectors.VectorSize;

            // Perform middle scans
            for (; this.VectorReadOffset < this.Region.Range - Vectors.VectorSize; this.VectorReadOffset += Vectors.VectorSize)
            {
                scanResults = Vector.BitwiseOr(this.VectorCompare(), sparseMask);
                this.EncodeScanResults(ref scanResults, ref sparseMask);
            }

            // Perform final scan, applying the overread mask if applicable.
            if (scanCount > 1)
            {
                scanResults = Vector.BitwiseAnd(overreadMask, Vector.BitwiseOr(this.VectorCompare(), sparseMask));
                this.EncodeScanResults(ref scanResults, ref sparseMask);
                this.VectorReadOffset += Vectors.VectorSize;
            }

            this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked();

            return this.RunLengthEncoder.GetCollectedRegions();
        }

        /// <summary>
        /// Run-length encodes the given scan results into snapshot regions.
        /// </summary>
        /// <param name="scanResults">The scan results to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void EncodeScanResults(ref Vector<Byte> scanResults, ref Vector<Byte> sparseMask)
        {
            // Optimization: check all vector results true
            if (Vector.GreaterThanAll(scanResults, Vector<Byte>.Zero))
            {
                this.RunLengthEncoder.EncodeRange(Vectors.VectorSize);
            }
            // Optimization: check all vector results false (ie equal to the sparse mask)
            else if (Vector.EqualsAll(scanResults, sparseMask))
            {
                this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked(Vectors.VectorSize);
            }
            else
            {
                // Otherwise the vector contains a mixture of true and false
                for (Int32 resultIndex = 0; resultIndex < Vectors.VectorSize; resultIndex += unchecked((Int32)this.Alignment))
                {
                    if (scanResults[resultIndex] != 0)
                    {
                        this.RunLengthEncoder.EncodeRange(unchecked((Int32)this.Alignment));
                    }
                    else
                    {
                        this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked(unchecked((Int32)this.Alignment));
                    }
                }
            }
        }
    }
    //// End class
}
//// End namespace
