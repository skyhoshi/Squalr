﻿namespace Squalr.Cli.CommandHandlers.Scan
{
    using CommandLine;
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.DataTypes;
    using Squalr.Engine.Memory;
    using Squalr.Engine.Scanning.Scanners;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using System;

    [Verb("", HelpText = "Attempts to crash the target program by zeroing out memory that changed recently.")]
    public class CrashCommandOptions
    {
        public Int32 Handle()
        {
            // If set to 0.0, assume unset and set to 1.0. Otherwise, clamp in bounds.
            Intensity = Intensity <= 0.0 ? 1.0 : Math.Clamp(Intensity, 0.0, 1.0);

            // Collect values
            TrackableTask<Snapshot> valueCollectorTask = ValueCollector.CollectValues(SessionManager.Session.OpenedProcess, SessionManager.Session.SnapshotManager.GetActiveSnapshotCreateIfNone(SessionManager.Session.OpenedProcess), TrackableTask.UniversalIdentifier);

            // Recollect values
            TrackableTask<Snapshot> valueRecollectorTask = ValueCollector.CollectValues(SessionManager.Session.OpenedProcess, valueCollectorTask.Result, TrackableTask.UniversalIdentifier);


            ScanConstraints scanConstraints = new ScanConstraints(DataTypeBase.Int32, new ScanConstraint(ScanConstraint.ConstraintType.Changed));
            // Scan for any changed values
            TrackableTask<Snapshot> scanTask = ManualScanner.Scan(valueRecollectorTask.Result, scanConstraints, TrackableTask.UniversalIdentifier);

            Random random = new Random();

            // Start overwriting any memory that changed with 0s
            foreach (SnapshotRegion region in scanTask.Result.SnapshotRegions)
            {
                for (Int32 index = 0; index < region.GetElementCount(DataTypeBase.Int32.Size); index++)
                {
                    if (random.NextDouble() <= Intensity)
                    {
                        MemoryWriter.Instance.Write<Int32>(SessionManager.Session.OpenedProcess, region[index].GetBaseAddress(Engine.Common.DataTypes.DataTypeBase.Int32.Size), 0);
                    }
                }
            }

            return 0;
        }

        [Value(0, MetaName = "intensity", HelpText = "How rigorous the memory overwriting should be. A value greater than 0.0 and less than or equal to 1.0.")]
        public Double Intensity { get; set; }
    }
    //// End class
}
//// End namespace
