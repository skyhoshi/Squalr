namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common;
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
        /// The read groups of this snapshot.
        /// </summary>
        private IEnumerable<ReadGroup> readGroups;

        /// <summary>
        /// The snapshot memory address alignment.
        /// </summary>
        private MemoryAlignment alignment = MemoryAlignment.Alignment1;

        // TODO: Not needed for current use cases, but it would be good to invoke this when proprties change.
        public event PropertyChangedEventHandler PropertyChanged;

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
            this.SetSnapshotRegions(snapshotRegions);
        }

        /// <summary>
        /// Gets the name associated with the method by which this snapshot was generated.
        /// </summary>
        public String SnapshotName { get; private set; }

        /// <summary>
        /// Gets the number of regions contained in this snapshot.
        /// </summary>
        /// <returns>The number of regions contained in this snapshot.</returns>
        public Int32 RegionCount { get; set; }

        /// <summary>
        /// Gets the total number of bytes contained in this snapshot.
        /// </summary>
        public UInt64 ByteCount { get; set; }

        /// <summary>
        /// Gets the number of individual elements contained in this snapshot.
        /// </summary>
        /// <returns>The number of individual elements contained in this snapshot.</returns>
        public UInt64 ElementCount { get; set; }

        /// <summary>
        /// Sets the label data type for all read groups.
        /// </summary>
        public ScannableType LabelDataType
        {
            set
            {
                this.ReadGroups.ForEach(readGroup => readGroup.LabelDataType = value);
            }
        }

        /// <summary>
        /// Gets the time since the last update was performed on this snapshot.
        /// </summary>
        public DateTime TimeSinceLastUpdate { get; private set; }

        /// <summary>
        /// Gets or sets the read groups of this snapshot.
        /// </summary>
        public IEnumerable<ReadGroup> ReadGroups
        {
            get
            {
                return this.readGroups;
            }

            set
            {
                this.readGroups = value;
            }
        }

        /// <summary>
        /// Gets or sets the snapshot memory address alignment.
        /// </summary>
        public MemoryAlignment Alignment
        {
            get
            {
                return this.alignment;
            }

            set
            {
                this.alignment = value;
                this.ReadGroups?.ForEach(readGroup => readGroup?.Align(this.alignment));
            }
        }

        /// <summary>
        /// Gets the read groups in this snapshot, ordered descending by their region size. This is much more performant for multi-threaded access.
        /// </summary>
        public IEnumerable<ReadGroup> OptimizedReadGroups
        {
            get
            {
                return this.ReadGroups?.OrderByDescending(readGroup => readGroup.RegionSize);
            }
        }

        /// <summary>
        /// Gets the snapshot regions in this snapshot. These are the same regions from the read groups, except flattened as an array.
        /// </summary>
        public SnapshotRegion[] SnapshotRegions { get; private set; }

        /// <summary>
        /// Gets the snapshot regions in this snapshot, ordered descending by their region size. This is much more performant for multi-threaded access.
        /// This is very similar to the greedy interval scheduling algorithm, and can result in significant scan speed gains.
        /// </summary>
        public IEnumerable<SnapshotRegion> OptimizedSnapshotRegions
        {
            get
            {
                return this.SnapshotRegions?.OrderByDescending(region => region.RegionSize);
            }
        }

        /// <summary>
        /// Indexer to allow the retrieval of the element at the specified index. This does NOT index into a region.
        /// </summary>
        /// <param name="elementIndex">The index of the snapshot element.</param>
        /// <returns>Returns the snapshot element at the specified index.</returns>
        public SnapshotElementIndexer this[UInt64 elementIndex, Int32 elementSize]
        {
            get
            {
                SnapshotRegion region = this.BinaryRegionSearch(elementIndex, elementSize);

                if (region == null)
                {
                    return null;
                }

                SnapshotElementIndexer indexer = region[(elementIndex - region.BaseElementIndex).ToInt32(), this.Alignment];

                return indexer;
            }
        }

        /// <summary>
        /// Sets the label of every element in this snapshot to the same value.
        /// </summary>
        /// <typeparam name="LabelType">The data type of the label.</typeparam>
        /// <param name="label">The new snapshot label value.</param>
        public void SetElementLabels<LabelType>(LabelType label) where LabelType : struct, IComparable<LabelType>
        {
            this.SnapshotRegions?.ForEach(x => x.ReadGroup.SetElementLabels(Enumerable.Repeat(label, unchecked((Int32)x.RegionSize)).Cast<Object>().ToArray()));
        }

        /// <summary>
        /// Adds snapshot regions to the regions contained in this snapshot.
        /// </summary>
        /// <param name="snapshotRegions">The snapshot regions to add.</param>
        public void SetSnapshotRegions(IEnumerable<SnapshotRegion> snapshotRegions)
        {
            this.ReadGroups = snapshotRegions.Select(x => x.ReadGroup).Distinct();
            this.SnapshotRegions = snapshotRegions.ToArray();
            this.TimeSinceLastUpdate = DateTime.Now;
            this.RegionCount = this.SnapshotRegions?.Count() ?? 0;
        }

        /// <summary>
        /// Determines how many elements are contained in this snapshot, and how many bytes total are contained.
        /// </summary>
        /// <param name="elementSize"></param>
        public void ComputeElementCount(Int32 elementSize)
        {
            this.ByteCount = 0;
            this.ElementCount = 0;

            this.SnapshotRegions?.ForEach(region =>
            {
                region.BaseElementIndex = this.ElementCount;
                this.ByteCount += region.RegionSize.ToUInt64();
                this.ElementCount += region.GetElementCount(elementSize, this.Alignment).ToUInt64();
            });
        }

        /// <summary>
        /// Determines if an address is contained in this snapshot.
        /// </summary>
        /// <param name="address">The address for which we are searching.</param>
        /// <returns>True if the address is contained.</returns>
        public Boolean ContainsAddress(UInt64 address)
        {
            if (this.SnapshotRegions == null || this.SnapshotRegions.Length == 0)
            {
                return false;
            }

            return this.ContainsAddressHelper(address, this.SnapshotRegions.Length / 2, 0, this.SnapshotRegions.Length);
        }

        /// <summary>
        /// Helper function for searching for an address in this snapshot. Binary search that assumes this snapshot has sorted regions.
        /// </summary>
        /// <param name="address">The address for which we are searching.</param>
        /// <param name="middle">The middle region index.</param>
        /// <param name="min">The lower region index.</param>
        /// <param name="max">The upper region index.</param>
        /// <returns>True if the address was found.</returns>
        private Boolean ContainsAddressHelper(UInt64 address, Int32 middle, Int32 min, Int32 max)
        {
            if (middle < 0 || middle == this.SnapshotRegions.Length || max < min)
            {
                return false;
            }

            if (address < this.SnapshotRegions[middle].BaseAddress)
            {
                return this.ContainsAddressHelper(address, (min + middle - 1) / 2, min, middle - 1);
            }
            else if (address > this.SnapshotRegions[middle].EndAddress)
            {
                return this.ContainsAddressHelper(address, (middle + 1 + max) / 2, middle + 1, max);
            }
            else
            {
                return true;
            }
        }

        private SnapshotRegion BinaryRegionSearch(UInt64 elementIndex, Int32 elementSize)
        {
            if (this.SnapshotRegions == null || this.SnapshotRegions.Length == 0)
            {
                return null;
            }

            return this.BinaryRegionSearchHelper(elementIndex, this.SnapshotRegions.Length / 2, 0, this.SnapshotRegions.Length, elementSize);
        }

        /// <summary>
        /// Helper function for searching for an address in this snapshot. Binary search that assumes this snapshot has sorted regions.
        /// </summary>
        /// <param name="elementIndex">The address for which we are searching.</param>
        /// <param name="middle">The middle region index.</param>
        /// <param name="min">The lower region index.</param>
        /// <param name="max">The upper region index.</param>
        /// <returns>True if the address was found.</returns>
        private SnapshotRegion BinaryRegionSearchHelper(UInt64 elementIndex, Int32 middle, Int32 min, Int32 max, Int32 elementSize)
        {
            if (middle < 0 || middle == this.SnapshotRegions.Length || max < min)
            {
                return null;
            }

            if (elementIndex < this.SnapshotRegions[middle].BaseElementIndex)
            {
                return this.BinaryRegionSearchHelper(elementIndex, (min + middle - 1) / 2, min, middle - 1, elementSize);
            }
            else if (elementIndex >= this.SnapshotRegions[middle].BaseElementIndex + this.SnapshotRegions[middle].GetElementCount(elementSize, this.Alignment).ToUInt64())
            {
                return this.BinaryRegionSearchHelper(elementIndex, (middle + 1 + max) / 2, middle + 1, max, elementSize);
            }
            else
            {
                return this.SnapshotRegions[middle];
            }
        }
    }
    //// End class
}
//// End namespace