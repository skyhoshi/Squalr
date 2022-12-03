namespace Squalr.Engine.Scanning.Snapshots
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.Logging;
    using Squalr.Engine.Memory;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// A static class for querying snapshots of memory from a target process.
    /// </summary>
    public static class SnapshotQuery
    {
        [Flags]
        public enum SnapshotRetrievalMode
        {
            FromSettings,
            FromUserModeMemory,
            FromHeaps,
            FromStack,
            FromModules,
        }

        /// <summary>
        /// Gets a snapshot based on the provided mode. Will not read any memory.
        /// </summary>
        /// <param name="snapshotCreationMode">The method of snapshot retrieval.</param>
        /// <returns>The collected snapshot.</returns>
        public static Snapshot GetSnapshot(Process process, SnapshotRetrievalMode snapshotCreationMode, EmulatorType emulatorType = EmulatorType.None)
        {
            switch (snapshotCreationMode)
            {
                case SnapshotRetrievalMode.FromSettings:
                    return SnapshotQuery.CreateSnapshotFromSettings(process, emulatorType);
                case SnapshotRetrievalMode.FromUserModeMemory:
                    return SnapshotQuery.CreateSnapshotFromUsermodeMemory(process);
                case SnapshotRetrievalMode.FromModules:
                    return SnapshotQuery.CreateSnapshotFromModules(process, emulatorType);
                case SnapshotRetrievalMode.FromHeaps:
                    return SnapshotQuery.CreateSnapshotFromHeaps(process, emulatorType);
                case SnapshotRetrievalMode.FromStack:
                    throw new NotImplementedException();
                default:
                    Logger.Log(LogLevel.Error, "Unknown snapshot retrieval mode");
                    return null;
            }
        }
        /// <summary>
        /// Creates a new snapshot of memory in the target process. Will not read any memory.
        /// </summary>
        /// <returns>The snapshot of memory taken in the target process.</returns>
        public static Snapshot CreateSnapshotByAddressRange(Process process, UInt64 startAddress, UInt64 endAddress)
        {
            MemoryProtectionEnum requiredPageFlags = 0;
            MemoryProtectionEnum excludedPageFlags = 0;
            MemoryTypeEnum allowedTypeFlags = MemoryTypeEnum.None | MemoryTypeEnum.Private | MemoryTypeEnum.Image | MemoryTypeEnum.Mapped;
            RegionBoundsHandling boundsHandling = RegionBoundsHandling.Resize;

            List<SnapshotRegion> readGroups = new List<SnapshotRegion>();
            IEnumerable<NormalizedRegion> virtualPages = MemoryQueryer.Instance.GetVirtualPages(
                process,
                requiredPageFlags,
                excludedPageFlags,
                allowedTypeFlags,
                startAddress,
                endAddress,
                boundsHandling);

            foreach (NormalizedRegion virtualPage in virtualPages)
            {
                virtualPage.Align(ScanSettings.Alignment);
                readGroups.Add(new SnapshotRegion(virtualPage.BaseAddress, virtualPage.RegionSize));
            }

            return new Snapshot(String.Empty, readGroups);
        }

        /// <summary>
        /// Creates a snapshot from all usermode memory. Will not read any memory.
        /// </summary>
        /// <returns>A snapshot created from usermode memory.</returns>
        private static Snapshot CreateSnapshotFromUsermodeMemory(Process process)
        {
            MemoryProtectionEnum requiredPageFlags = 0;
            MemoryProtectionEnum excludedPageFlags = 0;
            MemoryTypeEnum allowedTypeFlags = MemoryTypeEnum.None | MemoryTypeEnum.Private | MemoryTypeEnum.Image;

            UInt64 startAddress = 0;
            UInt64 endAddress = MemoryQueryer.Instance.GetMaxUsermodeAddress(process);

            List<SnapshotRegion> readGroups = new List<SnapshotRegion>();
            IEnumerable<NormalizedRegion> virtualPages = MemoryQueryer.Instance.GetVirtualPages(
                process,
                requiredPageFlags,
                excludedPageFlags,
                allowedTypeFlags,
                startAddress,
                endAddress);

            foreach (NormalizedRegion virtualPage in virtualPages)
            {
                virtualPage.Align(ScanSettings.Alignment);
                readGroups.Add(new SnapshotRegion(virtualPage.BaseAddress, virtualPage.RegionSize));
            }

            return new Snapshot(String.Empty, readGroups);
        }

        /// <summary>
        /// Creates a new snapshot of memory in the target process. Will not read any memory.
        /// </summary>
        /// <returns>The snapshot of memory taken in the target process.</returns>
        private static Snapshot CreateSnapshotFromSettings(Process process, EmulatorType emulatorType = EmulatorType.None)
        {
            List<SnapshotRegion> readGroups = new List<SnapshotRegion>();
            IEnumerable<NormalizedRegion> virtualPages;

            if (emulatorType == EmulatorType.Auto)
            {
                Logger.Log(LogLevel.Warn, "CreateSnapshotFromSettings called before the emulator type could be resolved. This may result in inaccurate results.");
            }

            // Fetch virtual pages based on settings
            switch (emulatorType)
            {
                case EmulatorType.Dolphin:
                    virtualPages = MemoryQueryer.Instance.GetEmulatorVirtualPages(process, emulatorType);
                    break;
                case EmulatorType.None:
                default:
                    MemoryProtectionEnum requiredPageFlags = SnapshotQuery.GetRequiredProtectionSettings();
                    MemoryProtectionEnum excludedPageFlags = SnapshotQuery.GetExcludedProtectionSettings();
                    MemoryTypeEnum allowedTypeFlags = SnapshotQuery.GetAllowedTypeSettings();

                    UInt64 startAddress;
                    UInt64 endAddress;

                    if (ScanSettings.IsUserMode)
                    {
                        startAddress = 0;
                        endAddress = MemoryQueryer.Instance.GetMaxUsermodeAddress(process);
                    }
                    else
                    {
                        startAddress = ScanSettings.StartAddress;
                        endAddress = ScanSettings.EndAddress;
                    }

                    virtualPages = MemoryQueryer.Instance.GetVirtualPages(
                        process,
                        requiredPageFlags,
                        excludedPageFlags,
                        allowedTypeFlags,
                        startAddress,
                        endAddress);
                    break;
            }

            // Convert each virtual page to a snapshot region
            foreach (NormalizedRegion virtualPage in virtualPages)
            {
                virtualPage.Align(ScanSettings.Alignment);
                readGroups.Add(new SnapshotRegion(virtualPage.BaseAddress, virtualPage.RegionSize));
            }

            return new Snapshot(String.Empty, readGroups);
        }

        /// <summary>
        /// Creates a snapshot from modules in the selected process.
        /// </summary>
        /// <returns>The created snapshot.</returns>
        private static Snapshot CreateSnapshotFromModules(Process process, EmulatorType emulatorType)
        {
            IEnumerable<SnapshotRegion> moduleRegions;

            if (emulatorType == EmulatorType.Auto)
            {
                Logger.Log(LogLevel.Warn, "CreateSnapshotFromModules called before the emulator type could be resolved. This may result in inaccurate results.");
            }

            switch (emulatorType)
            {
                case EmulatorType.Dolphin:
                    moduleRegions = MemoryQueryer.Instance.GetDolphinModules(process).Select(region => new SnapshotRegion(region.BaseAddress, region.RegionSize));
                    break;
                case EmulatorType.None:
                default:
                    moduleRegions = MemoryQueryer.Instance.GetModules(process).Select(region => new SnapshotRegion(region.BaseAddress, region.RegionSize));
                    break;
            }

            Snapshot moduleSnapshot = new Snapshot(null, moduleRegions);

            return moduleSnapshot;
        }

        /// <summary>
        /// Creates a snapshot from modules in the selected process.
        /// </summary>
        /// <returns>The created snapshot.</returns>
        private static Snapshot CreateSnapshotFromHeaps(Process process, EmulatorType emulatorType)
        {
            if (emulatorType == EmulatorType.Auto)
            {
                Logger.Log(LogLevel.Warn, "CreateSnapshotFromHeaps called before the emulator type could be resolved. This may result in inaccurate results.");
            }

            switch(emulatorType)
            {
                case EmulatorType.Dolphin:
                    List<SnapshotRegion> dolphinHeaps = new List<SnapshotRegion>();

                    foreach (NormalizedRegion virtualPage in MemoryQueryer.Instance.GetDolphinHeaps(process))
                    {
                        virtualPage.Align(ScanSettings.Alignment);
                        dolphinHeaps.Add(new SnapshotRegion(virtualPage.BaseAddress, virtualPage.RegionSize));
                    }

                    return new Snapshot(String.Empty, dolphinHeaps);
                case EmulatorType.None:
                default:
                    // This function implementation currently grabs all usermode memory and excludes modules. A better implementation would involve actually grabbing heaps.
                    Snapshot snapshot = SnapshotQuery.CreateSnapshotFromUsermodeMemory(process);
                    IEnumerable<NormalizedModule> modules = MemoryQueryer.Instance.GetModules(process);

                    MemoryProtectionEnum requiredPageFlags = 0;
                    MemoryProtectionEnum excludedPageFlags = 0;
                    MemoryTypeEnum allowedTypeFlags = MemoryTypeEnum.None | MemoryTypeEnum.Private | MemoryTypeEnum.Image;

                    UInt64 startAddress = 0;
                    UInt64 endAddress = MemoryQueryer.Instance.GetMaxUsermodeAddress(process);

                    List<SnapshotRegion> memoryRegions = new List<SnapshotRegion>();
                    IEnumerable<NormalizedRegion> virtualPages = MemoryQueryer.Instance.GetVirtualPages(
                        process,
                        requiredPageFlags,
                        excludedPageFlags,
                        allowedTypeFlags,
                        startAddress,
                        endAddress);

                    foreach (NormalizedRegion virtualPage in virtualPages)
                    {
                        if (modules.Any(x => x.BaseAddress == virtualPage.BaseAddress))
                        {
                            continue;
                        }

                        virtualPage.Align(ScanSettings.Alignment);
                        memoryRegions.Add(new SnapshotRegion(virtualPage.BaseAddress, virtualPage.RegionSize));
                    }

                    return new Snapshot(String.Empty, memoryRegions);
            }
        }

        /// <summary>
        /// Gets the allowed type settings for virtual memory queries based on the set type flags.
        /// </summary>
        /// <returns>The flags of the allowed types for virtual memory queries.</returns>
        private static MemoryTypeEnum GetAllowedTypeSettings()
        {
            MemoryTypeEnum result = 0;

            if (ScanSettings.MemoryTypeNone)
            {
                result |= MemoryTypeEnum.None;
            }

            if (ScanSettings.MemoryTypePrivate)
            {
                result |= MemoryTypeEnum.Private;
            }

            if (ScanSettings.MemoryTypeImage)
            {
                result |= MemoryTypeEnum.Image;
            }

            if (ScanSettings.MemoryTypeMapped)
            {
                result |= MemoryTypeEnum.Mapped;
            }

            return result;
        }

        /// <summary>
        /// Gets the required protection settings for virtual memory queries based on the set type flags.
        /// </summary>
        /// <returns>The flags of the required protections for virtual memory queries.</returns>
        private static MemoryProtectionEnum GetRequiredProtectionSettings()
        {
            MemoryProtectionEnum result = 0;

            if (ScanSettings.RequiredWrite)
            {
                result |= MemoryProtectionEnum.Write;
            }

            if (ScanSettings.RequiredExecute)
            {
                result |= MemoryProtectionEnum.Execute;
            }

            if (ScanSettings.RequiredCopyOnWrite)
            {
                result |= MemoryProtectionEnum.CopyOnWrite;
            }

            return result;
        }

        /// <summary>
        /// Gets the excluded protection settings for virtual memory queries based on the set type flags.
        /// </summary>
        /// <returns>The flags of the excluded protections for virtual memory queries.</returns>
        private static MemoryProtectionEnum GetExcludedProtectionSettings()
        {
            MemoryProtectionEnum result = 0;

            if (ScanSettings.ExcludedWrite)
            {
                result |= MemoryProtectionEnum.Write;
            }

            if (ScanSettings.ExcludedExecute)
            {
                result |= MemoryProtectionEnum.Execute;
            }

            if (ScanSettings.ExcludedCopyOnWrite)
            {
                result |= MemoryProtectionEnum.CopyOnWrite;
            }

            return result;
        }
    }
    //// End class
}
//// End namespace