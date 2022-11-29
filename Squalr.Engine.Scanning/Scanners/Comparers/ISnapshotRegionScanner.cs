namespace Squalr.Engine.Scanning.Scanners.Comparers
{
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An interface that defines the implementation for snapshot scans.
    /// </summary>
    internal unsafe interface ISnapshotRegionScanner : IDisposable
    {
        /// <summary>
        /// Performs a scan over the given region, returning the discovered regions.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public IList<SnapshotRegion> ScanRegion(SnapshotRegion region, ScanConstraints constraints);

        public void SetDisposeCallback(Action onDispose);
    }
    //// End interface
}
//// End namespace
