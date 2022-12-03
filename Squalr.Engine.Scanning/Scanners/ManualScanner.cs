namespace Squalr.Engine.Scanning.Scanners
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.Extensions;
    using Squalr.Engine.Common.Logging;
    using Squalr.Engine.Scanning.Scanners.Comparers;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using static Squalr.Engine.Common.TrackableTask;

    /// <summary>
    /// A memory scanning class for classic manual memory scanning techniques.
    /// </summary>
    public static class ManualScanner
    {
        /// <summary>
        /// The name of this scan.
        /// </summary>
        private const String Name = "Manual Scanner";

        /// <summary>
        /// Begins the manual scan based on the provided snapshot and parameters.
        /// </summary>
        /// <param name="snapshot">The snapshot on which to perfrom the scan.</param>
        /// <param name="constraints">The collection of scan constraints to use in the manual scan.</param>
        /// <param name="taskIdentifier">The unique identifier to prevent duplicate tasks.</param>
        /// <returns></returns>
        public static TrackableTask<Snapshot> Scan(Snapshot snapshot, ScanConstraints constraints, String taskIdentifier = null)
        {
            try
            {
                return TrackableTask<Snapshot>
                    .Create(ManualScanner.Name, taskIdentifier, out UpdateProgress updateProgress, out CancellationToken cancellationToken)
                    .With(Task<Snapshot>.Run(() =>
                    {
                        Snapshot result = null;

                        snapshot.AlignAndResolveAuto(constraints.Alignment, constraints.ElementType);

                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();

                            Int32 processedPages = 0;
                            ConcurrentScanRegionBag resultRegions = new ConcurrentScanRegionBag();

                            ParallelOptions options = ScanSettings.UseMultiThreadScans ? ParallelSettings.ParallelSettingsFastest : ParallelSettings.ParallelSettingsNone;

                            if (!ScanSettings.UseMultiThreadScans)
                            {
                                Logger.Log(LogLevel.Warn, "Multi-threaded scans are disabled in settings. Scan performance will be significantly decreased.");
                            }

                            options.CancellationToken = cancellationToken;

                            Parallel.ForEach(
                                snapshot.OptimizedSnapshotRegions,
                                options,
                                (region) =>
                                {
                                    // Check for canceled scan
                                    cancellationToken.ThrowIfCancellationRequested();

                                    if (!region.CanCompare(constraints: constraints))
                                    {
                                        return;
                                    }

                                    IList<IList<SnapshotElementRange>> discoveredElements = new List<IList<SnapshotElementRange>>();

                                    foreach (SnapshotElementRange elementRange in region)
                                    {
                                        using (ISnapshotRegionScanner scanner = SnapshotRegionScannerFactory.AquireScannerInstance(elementRange: elementRange, constraints: constraints))
                                        {
                                            IList<SnapshotElementRange> results = scanner.ScanRegion(elementRange: elementRange, constraints: constraints);

                                            if (!results.IsNullOrEmpty())
                                            {
                                                discoveredElements.Add(results);
                                            }
                                        }
                                    }

                                    // Update progress every N regions
                                    if (Interlocked.Increment(ref processedPages) % 32 == 0)
                                    {
                                        // Technically this callback is a data race, but it does not really matter if scan progress is not reported perfectly accurately
                                        updateProgress((float)processedPages / (float)snapshot.RegionCount * 100.0f);
                                    }
                                });
                            //// End foreach region

                            // Exit if canceled
                            cancellationToken.ThrowIfCancellationRequested();

                            result = new Snapshot(ManualScanner.Name, resultRegions);
                            result.AlignAndResolveAuto(constraints.Alignment, constraints.ElementType); // Bottleneck!
                            stopwatch.Stop();
                            Logger.Log(LogLevel.Info, "Scan complete in: " + stopwatch.Elapsed);
                            result.ComputeElementCount(constraints.ElementType.Size);
                            Logger.Log(LogLevel.Info, "Results: " + result.ElementCount + " (" + Conversions.ValueToMetricSize(result.ByteCount) + ")");
                        }
                        catch (OperationCanceledException ex)
                        {
                            Logger.Log(LogLevel.Warn, "Scan canceled", ex);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Error, "Error performing scan", ex);
                        }

                        return result;
                    }, cancellationToken));
            }
            catch (TaskConflictException ex)
            {
                Logger.Log(LogLevel.Warn, "Unable to start scan. Scan is already queued.");
                throw ex;
            }
        }
    }
    //// End class
}
//// End namespace