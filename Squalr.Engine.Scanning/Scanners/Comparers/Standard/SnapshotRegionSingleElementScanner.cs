namespace Squalr.Engine.Scanning.Scanners.Comparers.Standard
{
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A scanner that works by looping over each element of the snapshot individually. Much slower than the vectorized version.
    /// </summary>
    internal class SnapshotRegionSingleElementScanner : SnapshotRegionStandardScannerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegionSingleElementScanner" /> class.
        /// </summary>
        /// <param name="region">The parent region that contains this element.</param>
        /// <param name="constraints">The constraints to use for the element comparisons.</param>
        public unsafe SnapshotRegionSingleElementScanner() : base()
        {
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="SnapshotRegionSingleElementScanner" /> class.
        /// </summary>
        ~SnapshotRegionSingleElementScanner()
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
            this.InitializeNoPinning(region: region, constraints: constraints);

            fixed (Byte* currentValuePtr = &region.ReadGroup.CurrentValues[region.ReadGroupOffset])
            {
                if (region.ReadGroup.PreviousValues != null && region.ReadGroup.PreviousValues.Length > 0)
                {
                    fixed (Byte* previousValuePtr = &region.ReadGroup.PreviousValues[region.ReadGroupOffset])
                    {
                        this.CurrentValuePointer = currentValuePtr;
                        this.PreviousValuePointer = previousValuePtr;

                        if (this.ElementCompare())
                        {
                            return new List<SnapshotRegion>()
                            {
                                new SnapshotRegion(region.ReadGroup, region.ReadGroupOffset, region.Range)
                            };
                        }
                    }
                }
                else
                {
                    this.CurrentValuePointer = currentValuePtr;

                    if (this.ElementCompare())
                    {
                        return new List<SnapshotRegion>()
                        {
                            new SnapshotRegion(region.ReadGroup, region.ReadGroupOffset, region.Range)
                        };
                    }
                }
            }

            return null;
        }
    }
    //// End class
}
//// End namespace
