namespace Squalr.Engine.Scanning
{
    using Squalr.Engine.Common.Extensions;
    using Squalr.Engine.Scanning.Snapshots;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public class ConcurrentScanBag : ConcurrentBag<IList<SnapshotRegion>>, IEnumerable<SnapshotRegion>
    {
        IEnumerator<SnapshotRegion> IEnumerable<SnapshotRegion>.GetEnumerator()
        {
            foreach (IList<SnapshotRegion> list in this)
            {
                foreach (SnapshotRegion item in list)
                {
                    yield return item;
                }
            }
        }
    }
    //// End class
}
//// End namespace
