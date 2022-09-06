namespace Squalr.Engine.Scanning.Scanners
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.Logging;
    using Squalr.Engine.Processes;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using static Squalr.Engine.Common.TrackableTask;

    /// <summary>
    /// Collect values for a given snapshot. The values are assigned to a new snapshot.
    /// </summary>
    public static class ValueCollector
    {
        /// <summary>
        /// The name of this scan.
        /// </summary>
        private const String Name = "Value Collector";

        public static TrackableTask<Snapshot> CollectValues(Process process, Snapshot snapshot, String taskIdentifier = null, bool withLogging = true)
        {
            try
            {
                return TrackableTask<Snapshot>
                    .Create(ValueCollector.Name, taskIdentifier, out UpdateProgress updateProgress, out CancellationToken cancellationToken)
                    .With(Task<Snapshot>.Run(() =>
                    {
                        try
                        {
                            Int32 processedRegions = 0;

                            if (withLogging)
                            {
                                Logger.Log(LogLevel.Info, "Reading values from memory...");
                            }

                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();

                            ParallelOptions options = ParallelSettings.ParallelSettingsFastest.Clone();
                            options.CancellationToken = cancellationToken;

                            // Read memory to get current values for each region
                            Parallel.ForEach(
                                snapshot.OptimizedReadGroups,
                                options,
                                (readGroup) =>
                                {
                                    // Check for canceled scan
                                    cancellationToken.ThrowIfCancellationRequested();

                                    // Read the memory for this region
                                    readGroup.ReadAllMemory(process);

                                    // Update progress every N regions
                                    if (Interlocked.Increment(ref processedRegions) % 32 == 0)
                                    {
                                        updateProgress((float)processedRegions / (float)snapshot.RegionCount * 100.0f);
                                    }
                                });

                            // Exit if canceled
                            cancellationToken.ThrowIfCancellationRequested();

                            stopwatch.Stop();
                            snapshot.LoadMetaData(ScannableType.Byte.Size);

                            if (withLogging)
                            {
                                Logger.Log(LogLevel.Info, "Values collected in: " + stopwatch.Elapsed);
                                Logger.Log(LogLevel.Info, "Results: " + snapshot.ElementCount + " bytes (" + Conversions.ValueToMetricSize(snapshot.ByteCount) + ")");
                            }

                                return snapshot;
                        }
                        catch (OperationCanceledException ex)
                        {
                            if (withLogging)
                            {
                                Logger.Log(LogLevel.Warn, "Scan canceled", ex);
                            }

                            return null;
                        }
                        catch (Exception ex)
                        {
                            if (withLogging)
                            {
                                Logger.Log(LogLevel.Error, "Error performing scan", ex);
                            }

                            return null;
                        }
                    }, cancellationToken));
            }
            catch (TaskConflictException ex)
            {
                if (withLogging)
                {
                    Logger.Log(LogLevel.Warn, "Unable to start scan. Scan is already queued.");
                }

                throw ex;
            }
        }
    }
    //// End class
}
//// End namespace