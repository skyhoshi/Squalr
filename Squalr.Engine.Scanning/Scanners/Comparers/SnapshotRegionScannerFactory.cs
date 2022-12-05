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
        /// Performs a scan over the given element range, returning the elements that match the scan.
        /// </summary>
        /// <param name="elementRange">The element range to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting elements, if any.</returns>
        public static ISnapshotRegionScanner AquireScannerInstance(SnapshotElementRange elementRange, ScanConstraints constraints)
        {
            // This seems like it should save time, but it seems to lose substantial time
            /*if (unchecked(elementRange.Range == (Int32)constraints.Alignment))
            {
                return snapshotRegionSingleElementScannerPool.Get();
            }
            else */
            
            if (Vectors.HasVectorSupport && elementRange.ParentRegion.RegionSize >= Vectors.VectorSize)
            {
                return SnapshotRegionScannerFactory.CreateVectorScannerInstance(elementRange, constraints);
            }
            else
            {
                return snapshotRegionIterativeScannerPool.Get();
            }
        }

        /// <summary>
        /// Performs a scan over the given element range, returning the elements that match the scan.
        /// </summary>
        /// <param name="elementRange">The element range to scan.</param>
        /// <param name="constraints">The scan constraints.</param>
        /// <returns>The resulting elements, if any.</returns>
        public static ISnapshotRegionScanner CreateVectorScannerInstance(SnapshotElementRange region, ScanConstraints constraints)
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
