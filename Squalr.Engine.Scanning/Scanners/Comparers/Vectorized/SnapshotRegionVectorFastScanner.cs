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
    /// A fast vectorized snapshot region scanner that is optimized for snapshot regions that can be chunked to fit entirely in a hardware vector.
    /// </summary>
    internal unsafe class SnapshotRegionVectorFastScanner : SnapshotRegionVectorScannerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionVectorFastScanner" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The set of constraints to use for the element comparisons.</param>
        public SnapshotRegionVectorFastScanner() : base()
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
            this.Initialize(region : region, constraints: constraints);

            // This algorithm has three stages:
            // 1) Scan the first vector of memory, which may contain elements we do not care about. ie <x, x, x, x ... y, y, y, y>,
            //      where x is data outside the snapshot region (but within the readgroup), and y is within the region we are scanning.
            //      to solve this, we mask out the x values such that these will always be considered false by our scan
            // 2) Scan the middle parts of. These will all fit perfectly into vectors
            // 3) Scan the final vector, if it exists. This may spill outside of the snapshot region (but within the readgroup).
            //      This works exactly like the first scan, but reversed. ie <y, y, y, y, ... x, x, x, x>, where x values are masked to be false.
            //      Note: This mask may also be applied to the first scan, if it is also the last scan (ie only 1 scan total for this region).

            Int32 scanCount = this.Region.Range / Vectors.VectorSize + (this.VectorOverread > 0 ? 1 : 0);
            Vector<Byte> misalignmentMask = this.BuildVectorMisalignmentMask();
            Vector<Byte> overreadMask = this.BuildVectorOverreadMask();

            // Perform the first scan (there should always be at least one). Apply the misalignment mask, and optionally the overread mask if this is also the finals scan.
            Vector<Byte> scanResults = Vector.BitwiseAnd(Vector.BitwiseAnd(misalignmentMask, this.VectorCompare()), scanCount == 1 ? overreadMask : Vector<Byte>.One);
            this.EncodeScanResults(ref scanResults);
            this.VectorReadOffset += Vectors.VectorSize;

            // Perform middle scans
            for (; this.VectorReadOffset < this.Region.Range - Vectors.VectorSize; this.VectorReadOffset += Vectors.VectorSize)
            {
                scanResults = this.VectorCompare();
                this.EncodeScanResults(ref scanResults);
            }

            // Perform final scan, applying the overread mask if applicable.
            if (scanCount > 1)
            {
                scanResults = Vector.BitwiseAnd(overreadMask, this.VectorCompare());
                this.EncodeScanResults(ref scanResults);
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
        private void EncodeScanResults(ref Vector<Byte> scanResults)
        {
            // Optimization: check all vector results true
            if (Vector.GreaterThanAll(scanResults, Vector<Byte>.Zero))
            {
                this.RunLengthEncoder.EncodeRange(Vectors.VectorSize);
            }
            // Optimization: check all vector results false
            else if (Vector.EqualsAll(scanResults, Vector<Byte>.Zero))
            {
                this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked(Vectors.VectorSize);
            }
            else
            {
                // Otherwise the vector contains a mixture of true and false
                for (Int32 resultIndex = 0; resultIndex < Vectors.VectorSize; resultIndex += this.DataTypeSize)
                {
                    if (scanResults[resultIndex] != 0)
                    {
                        this.RunLengthEncoder.EncodeRange(this.DataTypeSize);
                    }
                    else
                    {
                        this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked(this.DataTypeSize);
                    }
                }
            }
        }
    }
    //// End class
}
//// End namespace
