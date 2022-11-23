namespace Squalr.Source.MemoryViewer
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Memory;
    using Squalr.Engine.Scanning.Scanners;
    using Squalr.Engine.Scanning.Snapshots;
    using Squalr.Source.Docking;
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// View model for the scan results.
    /// </summary>
    public class MemoryViewerViewModel : ToolViewModel
    {
        /// <summary>
        /// Singleton instance of the <see cref="MemoryViewerViewModel" /> class.
        /// </summary>
        private static Lazy<MemoryViewerViewModel> MemoryViewerViewModelInstance = new Lazy<MemoryViewerViewModel>(
                () => { return new MemoryViewerViewModel(); },
                LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// A memory stream of the data being viewed.
        /// </summary>
        private MemoryStream memoryStream;

        /// <summary>
        /// 
        /// </summary>
        Snapshot snapshot = null;

        ByteArrayType viewSize = new ByteArrayType(4096);

        /// <summary>
        /// 
        /// </summary>
        private UInt64 address = 0x80406F98;

        /// <summary>
        /// Prevents a default instance of the <see cref="MemoryViewerViewModel" /> class from being created.
        /// </summary>
        private MemoryViewerViewModel() : base("Memory Viewer")
        {
            this.RebuildSnapshot();

            DockingViewModel.GetInstance().RegisterViewModel(this);
            this.UpdateLoop();
        }

        /// <summary>
        /// Gets or sets the memory stream of the data being viewed.
        /// </summary>
        public MemoryStream MemoryStream
        {
            get
            {
                return this.memoryStream;
            }

            set
            {
                this.memoryStream = value;
                this.RaisePropertyChanged(nameof(this.MemoryStream));
            }
        }

        public UInt64 Address
        {
            get
            {
                return this.address;
            }

            set
            {
                this.address = value;
                this.RebuildSnapshot();
                this.RaisePropertyChanged(nameof(this.Address));
            }
        }

        /// <summary>
        /// Gets a singleton instance of the <see cref="MemoryViewerViewModel"/> class.
        /// </summary>
        /// <returns>A singleton instance of the class.</returns>
        public static MemoryViewerViewModel GetInstance()
        {
            return MemoryViewerViewModel.MemoryViewerViewModelInstance.Value;
        }

        /// <summary>
        /// Recieves an update of the active snapshot.
        /// </summary>
        /// <param name="snapshot">The active snapshot.</param>
        public void Update()
        {
            this.RebuildSnapshot();
            this.ReadMemoryViewerData();
        }

        /// <summary>
        /// Rebuilds the snapshot range based on the current address.
        /// </summary>
        private void RebuildSnapshot()
        {
            UInt64 effectiveAddress = this.Address;

            switch(SessionManager.Session.DetectedEmulator)
            {
                case EmulatorType.Dolphin:
                    effectiveAddress = MemoryQueryer.Instance.EmulatorAddressToRealAddress(SessionManager.Session?.OpenedProcess, this.Address, EmulatorType.Dolphin);
                    break;
            }

            this.snapshot = SnapshotQuery.CreateSnapshotByAddressRange(SessionManager.Session.OpenedProcess, effectiveAddress, effectiveAddress + (UInt64)this.viewSize.Length);
        }

        /// <summary>
        /// Reads all data to be shown in the memory viewer.
        /// </summary>
        private void ReadMemoryViewerData()
        {
            TrackableTask<Snapshot> valueCollectorTask = ValueCollector.CollectValues(
                SessionManager.Session.OpenedProcess,
                this.snapshot,
                withLogging: false);

            this.snapshot = valueCollectorTask.Result;

            Int32 size = (Int32)Math.Min((UInt64)this.viewSize.Length, this.snapshot.ByteCount);

            if (size <= 0)
            {
                this.MemoryStream = null;
            }
            else
            {
                SnapshotElementIndexer indexer = this.snapshot[0, size];

                if (indexer.HasCurrentValue())
                {
                    Byte[] value = indexer.LoadCurrentValue(this.viewSize) as Byte[];

                    if (value != null)
                    {
                        if (this.MemoryStream == null)
                        {
                            this.MemoryStream = new MemoryStream(value);
                        }
                        else
                        {
                            this.MemoryStream.Seek(0, SeekOrigin.Begin);
                            this.MemoryStream.Write(value, 0, (Int32)this.MemoryStream.Length);
                            this.RaisePropertyChanged(nameof(this.MemoryStream));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Runs the update loop, updating all scan results.
        /// </summary>
        private void UpdateLoop()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    this.Update();
                    Thread.Sleep(50);
                }
            });
        }
    }
    //// End class
}
//// End namespace