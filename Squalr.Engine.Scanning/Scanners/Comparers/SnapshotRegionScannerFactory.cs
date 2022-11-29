namespace Squalr.Engine.Scanning.Scanners.Comparers
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.DataStructures;
    using Squalr.Engine.Common.OS;
    using Squalr.Engine.Scanning.Scanners.Comparers.Iterative;
    using Squalr.Engine.Scanning.Scanners.Comparers.Vectorized;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;

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
        public static ISnapshotRegionScanner AquireScannerInstance(SnapshotRegion region, ScanConstraints constraints)
        {
            if (Vectors.HasVectorSupport)
            {
                return SnapshotRegionScannerFactory.CreateVectorScannerInstance(region, constraints);
            }
            else
            {
                return snapshotRegionIterativeScanners.Get();
            }
        }

        /// <summary>
        /// Creates the appropriate scanner class given the current scan constraints.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public static ISnapshotRegionScanner CreateVectorScannerInstance(SnapshotRegion region, ScanConstraints constraints)
        {
            switch (constraints?.ElementType)
            {
                case ByteArrayType:
                    return snapshotRegionVectorArrayOfBytesScanners.Get();
                default:
                    if ((Int32)constraints.Alignment == constraints.ElementType.Size)
                    {
                        return snapshotRegionVectorFastScanners.Get();
                    }
                    else
                    {
                        return snapshotRegionVectorMisalignedScanners.Get();
                    }
            }
        }

        private static ObjectPool<SnapshotRegionVectorArrayOfBytesScanner> snapshotRegionVectorArrayOfBytesScanners = new ObjectPool<SnapshotRegionVectorArrayOfBytesScanner>(() =>
        {
            SnapshotRegionVectorArrayOfBytesScanner instance = new SnapshotRegionVectorArrayOfBytesScanner();

            instance.SetDisposeCallback(() =>
            {
                snapshotRegionVectorArrayOfBytesScanners.Return(instance);
            });

            return instance;
        });

        private static ObjectPool<SnapshotRegionVectorFastScanner> snapshotRegionVectorFastScanners = new ObjectPool<SnapshotRegionVectorFastScanner>(() =>
        {
            SnapshotRegionVectorFastScanner instance = new SnapshotRegionVectorFastScanner();

            instance.SetDisposeCallback(() =>
            {
                snapshotRegionVectorFastScanners.Return(instance);
            });

            return instance;
        });

        private static ObjectPool<SnapshotRegionVectorMisalignedScanner> snapshotRegionVectorMisalignedScanners = new ObjectPool<SnapshotRegionVectorMisalignedScanner>(() =>
        {
            SnapshotRegionVectorMisalignedScanner instance = new SnapshotRegionVectorMisalignedScanner();

            instance.SetDisposeCallback(() =>
            {
                snapshotRegionVectorMisalignedScanners.Return(instance);
            });

            return instance;
        });

        private static ObjectPool<SnapshotRegionIterativeScanner> snapshotRegionIterativeScanners = new ObjectPool<SnapshotRegionIterativeScanner>(() =>
        {
            SnapshotRegionIterativeScanner instance = new SnapshotRegionIterativeScanner();

            instance.SetDisposeCallback(() =>
            {
                snapshotRegionIterativeScanners.Return(instance);
            });

            return instance;
        });
    }
    //// End class
}
//// End namespace
