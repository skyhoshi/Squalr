namespace Squalr.Engine.Scanning.Scanners.Comparers
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.OS;
    using Squalr.Engine.Scanning.Scanners.Comparers.Iterative;
    using Squalr.Engine.Scanning.Scanners.Comparers.Vectorized;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;

    /// <summary>
    /// A static class for creating the optimal scanner given the current scan constraints.
    /// </summary>
    internal static class SnapshotRegionScannerFactory
    {
        /// <summary>
        /// Creates the appropriate scanner class given the current scan constraints.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public static ISnapshotRegionScanner CreateScannerInstance(SnapshotRegion region, ScanConstraints constraints)
        {
            // TODO: Allocating these objects is a severe performance hit. Refactor to cache scanner instances, and lend these out to each scan thread temporarily.
            // Clean up when done with scan to avoid any refs to large scan regions.
            switch(constraints?.ElementType)
            {
                case ByteArrayType:
                    return new SnapshotRegionVectorArrayOfBytesScanner(region: region, constraints: constraints);
                default:
                    if (Vectors.HasVectorSupport)
                    {
                        return new SnapshotRegionVectorScanner(region: region, constraints: constraints);
                    }
                    else
                    {
                        return new SnapshotRegionIterativeScanner(region: region, constraints: constraints);
                    }
            }
        }
    }
    //// End class
}
//// End namespace
