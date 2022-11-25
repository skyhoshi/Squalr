namespace Squalr.Engine
{
    using Squalr.Engine.Processes;
    using Squalr.Engine.Scanning.Snapshots;
    using System.Diagnostics;

    /// <summary>
    /// Contains session information, including the target process in addition to snapshot history.
    /// </summary>
    public class Session : ProcessSession
    {
        public Session(Process processToOpen) : base(processToOpen)
        {
            this.SnapshotManager = new SnapshotManager();
        }

        public SnapshotManager SnapshotManager { get; private set; }
    }
    //// End class
}
//// End namespace
