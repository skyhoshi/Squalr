namespace Squalr.Engine.Scanning.Scanners.Comparers.Iterative
{
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A scanner that works by looping over each element of the snapshot individually. Much slower than the vectorized version.
    /// </summary>
    internal class SnapshotRegionIterativeScanner : SnapshotRegionStandardScannerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionIterativeScanner" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The constraints to use for the element comparisons.</param>
        public unsafe SnapshotRegionIterativeScanner() : base()
        {
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="SnapshotRegionIterativeScanner" /> class.
        /// </summary>
        ~SnapshotRegionIterativeScanner()
        {
        }

        /// <summary>
        /// Performs a scan over the given region, returning the discovered regions.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public unsafe override IList<SnapshotRegion> ScanRegion(SnapshotRegion region, ScanConstraints constraints)
        {
            this.Initialize(region: region, constraints: constraints);

            Int32 alignedElementCount = region.GetAlignedElementCount(constraints.Alignment);

            for (Int32 index = 0; index < alignedElementCount; index++)
            {
                if (this.ElementCompare())
                {
                    this.RunLengthEncoder.EncodeBatch((Int32)constraints.Alignment);
                }
                else
                {
                    this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked((Int32)constraints.Alignment);
                }

                this.CurrentValuePointer += (Int32)constraints.Alignment;
                this.PreviousValuePointer += (Int32)constraints.Alignment;
            }

            this.RunLengthEncoder.FinalizeCurrentEncodeUnchecked();

            return this.RunLengthEncoder.GetCollectedRegions();
        }
    }
    //// End class
}
//// End namespace
