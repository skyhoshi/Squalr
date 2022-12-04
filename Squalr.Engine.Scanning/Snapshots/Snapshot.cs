﻿namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.DataStructures;
    using Squalr.Engine.Common.Extensions;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;

    /// <summary>
    /// A class to contain snapshots of memory, which can be compared by scanners.
    /// </summary>
    public class Snapshot : INotifyPropertyChanged
    {
        /// <summary>
        /// The snapshot regions of this snapshot.
        /// </summary>
        private IEnumerable<SnapshotRegion> snapshotRegions;

        // TODO: Not needed for current use cases, but it would be good to invoke this when proprties change.
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="Snapshot" /> class.
        /// </summary>
        /// <param name="snapshotRegion">A single snapshot region with which to initialize this snapshot.</param>
        public Snapshot(SnapshotRegion snapshotRegion) : this(new SnapshotRegion[1] { snapshotRegion })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Snapshot" /> class.
        /// </summary>
        /// <param name="snapshotRegions">The regions with which to initialize this snapshot.</param>
        public Snapshot(IEnumerable<SnapshotRegion> snapshotRegions) : this(String.Empty, snapshotRegions)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Snapshot" /> class.
        /// </summary>
        /// <param name="snapshotRegions">The regions with which to initialize this snapshot.</param>
        /// <param name="snapshotName">The snapshot generation method name.</param>
        public Snapshot(String snapshotName, IEnumerable<SnapshotRegion> snapshotRegions)
        {
            this.SnapshotName = snapshotName ?? String.Empty;
            this.SetSnapshotRegions(snapshotRegions?.ToArray());  // TODO: Temp ToArray to test pointer scan glitchyness
        }

        /// <summary>
        /// Gets or sets the name associated with this snapshot. Usually this is the method by which this snapshot was generated.
        /// </summary>
        public String SnapshotName { get; set; }

        /// <summary>
        /// Gets the number of regions contained in this snapshot.
        /// </summary>
        /// <returns>The number of regions contained in this snapshot.</returns>
        public Int32 RegionCount { get; private set; }

        /// <summary>
        /// Gets the total number of bytes contained in this snapshot.
        /// </summary>
        public UInt64 ByteCount { get; private set; }

        /// <summary>
        /// Gets the number of individual elements contained in this snapshot.
        /// </summary>
        /// <returns>The number of individual elements contained in this snapshot.</returns>
        public UInt64 ElementCount { get; private set; }

        /// <summary>
        /// Gets the time since the last update was performed on this snapshot.
        /// </summary>
        public DateTime TimeSinceLastUpdate { get; private set; }

        /// <summary>
        /// Gets the alignment of the elements within this snapshot.
        /// </summary>
        public MemoryAlignment Alignment { get; private set; }

        /// <summary>
        /// Gets or sets the snapshot regions contained by this snapshot.
        /// </summary>
        public IEnumerable<SnapshotRegion> SnapshotRegions
        {
            get
            {
                return this.snapshotRegions;
            }

            set
            {
                this.snapshotRegions = value;
            }
        }

        /// <summary>
        /// Gets the read groups in this snapshot, ordered descending by their region size. This is slightly more performant for reading values, as larger more intensive regions get read first.
        /// This allows for a greedy scheduling algorithm and beter multi-thread resource utilization.
        /// </summary>
        public IEnumerable<SnapshotRegion> ReadOptimizedSnapshotRegions
        {
            get
            {
                return this.SnapshotRegions?.OrderByDescending(snapshotRegion => snapshotRegion.RegionSize);
            }
        }

        /// <summary>
        /// Gets or sets a lookup table used for querying scan results quickly.
        /// </summary>
        private IntervalTree<UInt64, SnapshotRegion> SnapshotRegionIndexLookupTable { get; set; }

        /// <summary>
        /// Indexer to allow the retrieval of the element at the specified index. This does NOT index into a region.
        /// </summary>
        /// <param name="elementIndex">The index of the snapshot element.</param>
        /// <returns>Returns the snapshot element at the specified index.</returns>
        public SnapshotElementIndexer this[UInt64 elementIndex, MemoryAlignment alignment]
        {
            get
            {
                // Build the index lookup table if needed
                if (this.SnapshotRegionIndexLookupTable == null || this.SnapshotRegionIndexLookupTable.Count <= 0)
                {
                    this.BuildLookupTable(alignment);
                }

                SnapshotRegion region = this.SnapshotRegionIndexLookupTable.QueryOne(elementIndex);

                if (region == null)
                {
                    return null;
                }

                SnapshotElementIndexer indexer = region[elementIndex, alignment];

                return indexer;
            }
        }

        /// <summary>
        /// Adds snapshot regions to the regions contained in this snapshot.
        /// </summary>
        /// <param name="snapshotRegions">The snapshot regions to add.</param>
        public void SetSnapshotRegions(IEnumerable<SnapshotRegion> snapshotRegions)
        {
            this.SnapshotRegions = snapshotRegions;
            this.TimeSinceLastUpdate = DateTime.Now;
            this.RegionCount = this.SnapshotRegions?.Count() ?? 0;
        }

        /// <summary>
        /// Determines how many elements are contained in this snapshot, and how many bytes total are contained.
        /// </summary>
        public void ComputeElementAndByteCounts(MemoryAlignment alignment)
        {
            this.Alignment = alignment;
            this.ByteCount = 0;
            this.ElementCount = 0;
            this.SnapshotRegionIndexLookupTable?.Clear();

            this.SnapshotRegions.OrderBy(region => region.BaseAddress)?.ForEach(region =>
            {
                region.BaseElementIndex = this.ElementCount;
                this.ByteCount += region.ElementByteCount.ToUInt64();
                this.ElementCount += region.TotalElementCount.ToUInt64();
            });
        }

        /// <summary>
        /// Determines how many elements are contained in this snapshot, and how many bytes total are contained.
        /// </summary>
        public void ComputeElementAndByteCountsCascading(Int32 dataTypeSize, MemoryAlignment alignment)
        {
            this.Alignment = alignment;
            this.ByteCount = 0;
            this.ElementCount = 0;
            this.SnapshotRegionIndexLookupTable?.Clear();

            this.SnapshotRegions.OrderBy(region => region.BaseAddress)?.ForEach(region =>
            {
                region.ComputeByteAndElementCounts(dataTypeSize, alignment);
                region.BaseElementIndex = this.ElementCount;
                this.ByteCount += region.ElementByteCount.ToUInt64();
                this.ElementCount += region.TotalElementCount.ToUInt64();
            });
        }

        /// <summary>
        /// Builds the element index lookup table for this snapshot. Used to display scan results.
        /// </summary>
        /// <param name="alignment">The alignment of the elements in this snapshot region.</param>
        private void BuildLookupTable(MemoryAlignment alignment)
        {
            if (this.SnapshotRegionIndexLookupTable == null)
            {
                this.SnapshotRegionIndexLookupTable = new IntervalTree<UInt64, SnapshotRegion>();
            }
            else
            {
                this.SnapshotRegionIndexLookupTable.Clear();
            }

            this.SnapshotRegions?.ForEach(region =>
            {
                this.SnapshotRegionIndexLookupTable.Add(region.BaseElementIndex, region.BaseElementIndex + region.TotalElementCount.ToUInt64() - 1, region);
            });
        }
    }
    //// End class
}
//// End namespace