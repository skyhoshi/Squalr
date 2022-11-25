namespace Squalr.Engine.Scanning.Scanners.Pointers.SearchKernels
{
    using Squalr.Engine.Scanning.Scanners.Comparers;
    using System;
    using System.Numerics;

    /// <summary>
    /// Defines an interface for an object that can search for pointers that point within a specified offset of a given set of snapshot regions.
    /// </summary>
    internal interface IPointerSearchKernel
    {
        Func<Vector<Byte>> GetSearchKernel(ISnapshotRegionScanner snapshotRegionScanner);
    }
    //// End interface
}
//// End namespace