namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common.DataTypes;
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
        private IList<ReadGroup> readGroups;

        // TODO: Not needed for current use cases, but it would be good to invoke this when proprties change.
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="Snapshot" /> class.
        /// </summary>
        /// <param name="memoryRegions">The regions with which to initialize this snapshot.</param>
        public Snapshot(string snapshotName, IList<ReadGroup> memoryRegions = null) : this(snapshotName, memoryRegions?.SelectMany(readGroup => readGroup.SnapshotRegions))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Snapshot" /> class.
        /// </summary>
        /// <param name="snapshotRegions">The regions with which to initialize this snapshot.</param>
        public Snapshot(IEnumerable<SnapshotRegion> snapshotRegions) : this(string.Empty, snapshotRegions)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Snapshot" /> class.
        /// </summary>
        /// <param name="snapshotRegions">The regions with which to initialize this snapshot.</param>
        /// <param name="snapshotName">The snapshot generation method name.</param>
        public Snapshot(string snapshotName, IEnumerable<SnapshotRegion> snapshotRegions)
        {
            this.SnapshotName = snapshotName ?? string.Empty;
            this.SetSnapshotRegions(snapshotRegions);
        }

        /// <summary>
        /// Gets the name associated with the method by which this snapshot was generated.
        /// </summary>
        public string SnapshotName { get; private set; }

        /// <summary>
        /// Gets the number of regions contained in this snapshot.
        /// </summary>
        /// <returns>The number of regions contained in this snapshot.</returns>
        public int RegionCount { get; set; }

        /// <summary>
        /// Gets the total number of bytes contained in this snapshot.
        /// </summary>
        public ulong ByteCount { get; set; }

        /// <summary>
        /// Gets the number of individual elements contained in this snapshot.
        /// </summary>
        /// <returns>The number of individual elements contained in this snapshot.</returns>
        public ulong ElementCount { get; set; }

        /// <summary>
        /// Sets the label data type for all read groups.
        /// </summary>
        public DataTypeBase LabelDataType
        {
            set
            {
                this.ReadGroups.ForEach(readGroup => readGroup.LabelDataType = value);
            }
        }

        /// <summary>
        /// Updates the base address of each region in this snapshot to match the provided alignment.
        /// </summary>
        /// <param name="alignment">The base address alignment.</param>
        public void Align(int alignment)
        {
            this.ReadGroups.ForEach(readGroup => readGroup.Align(alignment));
        }

        /// <summary>
        /// Gets the time since the last update was performed on this snapshot.
        /// </summary>
        public DateTime TimeSinceLastUpdate { get; private set; }

        /// <summary>
        /// Gets or sets the read groups of this snapshot.
        /// </summary>
        public IList<ReadGroup> ReadGroups
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
        public SnapshotElementIndexer this[ulong elementIndex, int elementSize = 1]
        {
            get
            {
                SnapshotRegion region = this.BinaryRegionSearch(elementIndex, elementSize);

                if (region == null)
                {
                    return null;
                }

                return region[(elementIndex - region.BaseElementIndex).ToInt32()];
            }
        }

        /// <summary>
        /// Creates a shallow clone of this snapshot.
        /// </summary>
        /// <param name="newSnapshotName">The snapshot generation method name.</param>
        /// <returns>The shallow cloned snapshot.</returns>
        public Snapshot Clone(string newSnapshotName = null)
        {
            return new Snapshot(newSnapshotName, this.ReadGroups);
        }

        /// <summary>
        /// Sets the label of every element in this snapshot to the same value.
        /// </summary>
        /// <typeparam name="LabelType">The data type of the label.</typeparam>
        /// <param name="label">The new snapshot label value.</param>
        public void SetElementLabels<LabelType>(LabelType label) where LabelType : struct, IComparable<LabelType>
        {
            this.SnapshotRegions?.ForEach(x => x.ReadGroup.SetElementLabels(Enumerable.Repeat(label, unchecked((int)(x.RegionSize))).Cast<object>().ToArray()));
        }

        /// <summary>
        /// Adds snapshot regions to the regions contained in this snapshot.
        /// </summary>
        /// <param name="snapshotRegions">The snapshot regions to add.</param>
        public void SetSnapshotRegions(IEnumerable<SnapshotRegion> snapshotRegions)
        {
            IEnumerable<IGrouping<ReadGroup, SnapshotRegion>> snapshotsByReadGroup = snapshotRegions.GroupBy(region => region.ReadGroup);

            foreach (IGrouping<ReadGroup, SnapshotRegion> group in snapshotsByReadGroup)
            {
                group.Key.SnapshotRegions = group.OrderBy(region => region.ReadGroupOffset);
            }

            this.ReadGroups = snapshotsByReadGroup.Select(x => x.Key).OrderBy(group => group.BaseAddress).ToList();
            this.SnapshotRegions = snapshotRegions.ToArray();
            this.TimeSinceLastUpdate = DateTime.Now;
            this.SnapshotRegions = snapshotRegions.ToArray();
            this.RegionCount = this.SnapshotRegions?.Count() ?? 0;
        }

        public void LoadMetaData(int elementSize)
        {
            this.ByteCount = 0;
            this.ElementCount = 0;

            this.SnapshotRegions?.ForEach(region =>
            {
                region.BaseElementIndex = this.ElementCount;
                this.ByteCount += region.RegionSize.ToUInt64();
                this.ElementCount += region.GetElementCount(elementSize).ToUInt64();
            });
        }

        /// <summary>
        /// Determines if an address is contained in this snapshot.
        /// </summary>
        /// <param name="address">The address for which we are searching.</param>
        /// <returns>True if the address is contained.</returns>
        public bool ContainsAddress(ulong address)
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
        private bool ContainsAddressHelper(ulong address, int middle, int min, int max)
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

        private SnapshotRegion BinaryRegionSearch(ulong elementIndex, int elementSize)
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
        private SnapshotRegion BinaryRegionSearchHelper(ulong elementIndex, int middle, int min, int max, int elementSize)
        {
            if (middle < 0 || middle == this.SnapshotRegions.Length || max < min)
            {
                return null;
            }

            if (elementIndex < this.SnapshotRegions[middle].BaseElementIndex)
            {
                return this.BinaryRegionSearchHelper(elementIndex, (min + middle - 1) / 2, min, middle - 1, elementSize);
            }
            else if (elementIndex >= this.SnapshotRegions[middle].BaseElementIndex + this.SnapshotRegions[middle].GetElementCount(elementSize).ToUInt64())
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