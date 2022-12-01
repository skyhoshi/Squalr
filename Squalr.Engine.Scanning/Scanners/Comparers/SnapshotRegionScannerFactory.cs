namespace Squalr.Engine.Scanning.Scanners.Comparers
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.DataStructures;
    using Squalr.Engine.Common.Hardware;
    using Squalr.Engine.Scanning.Scanners.Comparers.Standard;
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
        /// Creates the appropriate scanner class given the current scan constraints. Different scanner implementations work best in different scenarios.
        /// This method will automatically select the best scanner for the given workload.
        /// </summary>
        /// <param name="region">The region to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting regions, if any.</returns>
        public static ISnapshotRegionScanner AquireScannerInstance(SnapshotRegion region, ScanConstraints constraints)
        {
            if (region.Range == 1)
            {
                return snapshotRegionSingleElementScannerPool.Get();
            }
            else if (Vectors.HasVectorSupport && region.ReadGroup.RegionSize >= Vectors.VectorSize)
            {
                return SnapshotRegionScannerFactory.CreateVectorScannerInstance(region, constraints);
            }
            else
            {
                return snapshotRegionIterativeScannerPool.Get();
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
                    return snapshotRegionVectorArrayOfBytesScannerPool.Get();
                default:
                    Int32 alignmentSize = unchecked((Int32)constraints.Alignment);

                    if (alignmentSize == constraints.ElementType.Size)
                    {
                        return snapshotRegionVectorFastScannerPool.Get();
                    }
                    else if (alignmentSize > constraints.ElementType.Size)
                    {
                        return snapshotRegionVectorSparseScannerPool.Get();
                    }
                    else
                    {
                        return snapshotRegionVectorStaggeredScannerPool.Get();
                    }
            }
        }

        private static readonly ObjectPool<SnapshotRegionSingleElementScanner> snapshotRegionSingleElementScannerPool = new ObjectPool<SnapshotRegionSingleElementScanner>(() =>
        {
            SnapshotRegionSingleElementScanner instance = new SnapshotRegionSingleElementScanner();

            instance.SetDisposeCallback(() =>
            {
                snapshotRegionSingleElementScannerPool.Return(instance);
            });

            return instance;
        });

        private static readonly ObjectPool<SnapshotRegionVectorArrayOfBytesScanner> snapshotRegionVectorArrayOfBytesScannerPool = new ObjectPool<SnapshotRegionVectorArrayOfBytesScanner>(() =>
        {
            SnapshotRegionVectorArrayOfBytesScanner instance = new SnapshotRegionVectorArrayOfBytesScanner();

            instance.SetDisposeCallback(() =>
            {
                snapshotRegionVectorArrayOfBytesScannerPool.Return(instance);
            });

            return instance;
        });

        private static readonly ObjectPool<SnapshotRegionVectorSparseScanner> snapshotRegionVectorSparseScannerPool = new ObjectPool<SnapshotRegionVectorSparseScanner>(() =>
        {
            SnapshotRegionVectorSparseScanner instance = new SnapshotRegionVectorSparseScanner();

            instance.SetDisposeCallback(() =>
            {
                snapshotRegionVectorSparseScannerPool.Return(instance);
            });

            return instance;
        });

        private static readonly ObjectPool<SnapshotRegionVectorFastScanner> snapshotRegionVectorFastScannerPool = new ObjectPool<SnapshotRegionVectorFastScanner>(() =>
        {
            SnapshotRegionVectorFastScanner instance = new SnapshotRegionVectorFastScanner();

            instance.SetDisposeCallback(() =>
            {
                snapshotRegionVectorFastScannerPool.Return(instance);
            });

            return instance;
        });

        private static readonly ObjectPool<SnapshotRegionVectorStaggeredScanner> snapshotRegionVectorStaggeredScannerPool = new ObjectPool<SnapshotRegionVectorStaggeredScanner>(() =>
        {
            SnapshotRegionVectorStaggeredScanner instance = new SnapshotRegionVectorStaggeredScanner();

            instance.SetDisposeCallback(() =>
            {
                snapshotRegionVectorStaggeredScannerPool.Return(instance);
            });

            return instance;
        });

        private static readonly ObjectPool<SnapshotRegionIterativeScanner> snapshotRegionIterativeScannerPool = new ObjectPool<SnapshotRegionIterativeScanner>(() =>
        {
            SnapshotRegionIterativeScanner instance = new SnapshotRegionIterativeScanner();

            instance.SetDisposeCallback(() =>
            {
                snapshotRegionIterativeScannerPool.Return(instance);
            });

            return instance;
        });
    }
    //// End class
}
//// End namespace
