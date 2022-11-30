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

            // Perform the first (potentially misaligned) vector scan. There should always be 1 scan needed, so no checks required.
            Vector<Byte> misalignmentMask = this.BuildVectorMisalignmentMask();
            Vector<Byte> scanResults = Vector.BitwiseAnd(misalignmentMask, this.VectorCompare());
            this.EncodeScanResults(ref scanResults);
            this.VectorReadOffset += Vectors.VectorSize;

            // Perform remaining scans
            for (; this.VectorReadOffset < this.Region.Range; this.VectorReadOffset += Vectors.VectorSize)
            {
                scanResults = this.VectorCompare();
                this.EncodeScanResults(ref scanResults);
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
