namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common.Hardware;
    using Squalr.Engine.Memory;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// Defines a segment of process memory, which many snapshot sub regions may read from. This serves as a shared pool of memory, such as to
    /// minimize the number of calls to the OS to read the memory of a process.
    /// </summary>
    public class SnapshotRegion : NormalizedRegion, IEnumerable<SnapshotElementRange>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegion" /> class.
        /// </summary>
        /// <param name="baseAddress">The base address of this memory region.</param>
        /// <param name="regionSize">The size of this memory region.</param>
        public SnapshotRegion(UInt64 baseAddress, Int32 regionSize) : base(baseAddress, regionSize)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapshotRegion" /> class.
        /// </summary>
        /// <param name="baseAddress">The base address of this memory region.</param>
        /// <param name="regionSize">The size of this memory region.</param>
        public SnapshotRegion(UInt64 baseAddress, Byte[] initialBytes) : base(baseAddress, initialBytes.Length)
        {
            this.CurrentValues = initialBytes;
        }

        /// <summary>
        /// Gets the most recently read values.
        /// </summary>
        public unsafe Byte[] CurrentValues { get; private set; }

        /// <summary>
        /// Gets the previously read values.
        /// </summary>
        public unsafe Byte[] PreviousValues { get; private set; }

        /// <summary>
        /// Gets the snapshot element ranges in this snapshot. These are elements discovered by scans.
        /// </summary>
        public SnapshotElementRange[] SnapshotElementRanges { get; private set; }

        /// <summary>
        /// Reads all memory for this memory region.
        /// </summary>
        /// <returns>The bytes read from memory.</returns>
        public unsafe Boolean ReadAllMemory(Process process)
        {
            this.SetPreviousValues(this.CurrentValues);
            this.SetCurrentValues(MemoryReader.Instance.ReadBytes(process, this.BaseAddress, this.RegionSize, out bool readSuccess));

            if (!readSuccess)
            {
                this.SetPreviousValues(null);
                this.SetCurrentValues(null);
            }

            return readSuccess;
        }

        /// <summary>
        /// Determines if a relative comparison can be done for this region, ie current and previous values are loaded.
        /// </summary>
        /// <param name="constraints">The collection of scan constraints to use in the manual scan.</param>
        /// <returns>True if a relative comparison can be done for this region.</returns>
        public Boolean CanCompare(IScanConstraint constraints)
        {
            if (constraints == null
                || !constraints.IsValid()
                || this.CurrentValues == null
                || ((constraints as ScanConstraint)?.IsRelativeConstraint() ?? false) && this.PreviousValues == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sets the current values of this region.
        /// </summary>
        /// <param name="newValues">The raw bytes of the values.</param>
        public void SetCurrentValues(Byte[] newValues)
        {
            this.CurrentValues = newValues;
        }

        /// <summary>
        /// Sets the previous values of this region.
        /// </summary>
        /// <param name="newValues">The raw bytes of the values.</param>
        public void SetPreviousValues(Byte[] newValues)
        {
            this.PreviousValues = newValues;
        }

        /// <summary>
        /// Gets an enumerator for all snapshot element ranges within this snapshot region.
        /// </summary>
        /// <returns>An enumerator for all snapshot element ranges within this snapshot region.</returns>
        public IEnumerator<SnapshotElementRange> GetEnumerator()
        {
            return SnapshotElementRanges?.AsEnumerable()?.GetEnumerator();
        }

        /// <summary>
        /// Gets an enumerator for all snapshot element ranges within this snapshot region.
        /// </summary>
        /// <returns>An enumerator for all snapshot element ranges within this snapshot region.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return SnapshotElementRanges?.GetEnumerator();
        }
    }
    //// End class
}
//// End namespace