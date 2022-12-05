namespace Squalr.Source.MemoryViewer
{
    using GalaSoft.MvvmLight.Command;
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.Extensions;
    using Squalr.Engine.Memory;
    using Squalr.Engine.Scanning.Snapshots;
    using Squalr.Source.Docking;
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    /// <summary>
    /// View model for the scan results.
    /// </summary>
    public class MemoryViewerViewModel : ToolViewModel
    {
        /// <summary>
        /// Singleton instance of the <see cref="MemoryViewerViewModel" /> class.
        /// </summary>
        private static readonly Lazy<MemoryViewerViewModel> memoryViewerViewModelInstance = new Lazy<MemoryViewerViewModel>(
                () => { return new MemoryViewerViewModel(); },
                LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// A memory stream of the data being viewed.
        /// </summary>
        private MemoryStream memoryStream;

        /// <summary>
        /// The current snapshot region page.
        /// </summary>
        private Int32 currentPage;

        /// <summary>
        /// 
        /// </summary>
        SnapshotRegion[] snapshotRegions;

        SnapshotRegion activeRegion;

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
            this.FirstPageCommand = new RelayCommand(() => Task.Run(() => this.FirstPage()), () => true);
            this.LastPageCommand = new RelayCommand(() => Task.Run(() => this.LastPage()), () => true);
            this.PreviousPageCommand = new RelayCommand(() => Task.Run(() => this.PreviousPage()), () => true);
            this.NextPageCommand = new RelayCommand(() => Task.Run(() => this.NextPage()), () => true);

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

        /// <summary>
        /// Gets the command to go to the first page.
        /// </summary>
        public ICommand FirstPageCommand { get; private set; }

        /// <summary>
        /// Gets the command to go to the last page.
        /// </summary>
        public ICommand LastPageCommand { get; private set; }

        /// <summary>
        /// Gets the command to go to the previous page.
        /// </summary>
        public ICommand PreviousPageCommand { get; private set; }

        /// <summary>
        /// Gets the command to go to the next page.
        /// </summary>
        public ICommand NextPageCommand { get; private set; }

        /// <summary>
        /// Gets or sets the current page from which can results are loaded.
        /// </summary>
        public Int32 CurrentPage
        {
            get
            {
                return this.currentPage;
            }

            set
            {
                this.currentPage = value;
                this.MemoryStream = null;
                this.RefreshCurrentView();
                this.RefreshUIBindings();
            }
        }

        /// <summary>
        /// Gets a value indicating whether first page navigation is available.
        /// </summary>
        public Boolean CanNavigateFirst
        {
            get
            {
                return this.PageCount > 0 && this.CurrentPage > 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether next page navigation is available.
        /// </summary>
        public Boolean CanNavigateNext
        {
            get
            {
                return this.CurrentPage < this.PageCount;
            }
        }

        /// <summary>
        /// Gets a value indicating whether previous page navigation is available.
        /// </summary>
        public Boolean CanNavigatePrevious
        {
            get
            {
                return this.CurrentPage > 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether last page navigation is available.
        /// </summary>
        public Boolean CanNavigateLast
        {
            get
            {
                return this.PageCount > 0 && this.CurrentPage != this.PageCount;
            }
        }

        /// <summary>
        /// Gets the total number of pages of scan results found.
        /// </summary>
        public Int32 PageCount
        {
            get
            {
                return this.snapshotRegions == null ? 0 : (this.snapshotRegions.Length - 1);
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
            return MemoryViewerViewModel.memoryViewerViewModelInstance.Value;
        }

        /// <summary>
        /// Goes to the first page of results.
        /// </summary>
        private void FirstPage()
        {
            this.CurrentPage = 0;
        }

        /// <summary>
        /// Goes to the last page of results.
        /// </summary>
        private void LastPage()
        {
            this.CurrentPage = this.PageCount;
        }

        /// <summary>
        /// Goes to the previous page of results.
        /// </summary>
        private void PreviousPage()
        {
            this.CurrentPage = (this.CurrentPage - 1).Clamp(0, this.PageCount);
        }

        /// <summary>
        /// Goes to the next page of results.
        /// </summary>
        private void NextPage()
        {
            this.CurrentPage = (this.CurrentPage + 1).Clamp(0, this.PageCount);
        }

        /// <summary>
        /// Rebuilds the snapshot range based on the current address.
        /// </summary>
        private void RebuildSnapshot()
        {
            UInt64 effectiveAddress = this.Address;

            switch(SessionManager.Session.DetectedEmulator)
            {
                // TODO: Probably do not need special cases for emu, just make sure SnapshotQuery.SnapshotRetrievalMode.FromUserModeMemory returns what we want
                case EmulatorType.Dolphin:
                    // effectiveAddress = MemoryQueryer.Instance.EmulatorAddressToRealAddress(SessionManager.Session?.OpenedProcess, this.Address, EmulatorType.Dolphin);
                    // this.snapshot = SnapshotQuery.CreateSnapshotByAddressRange(SessionManager.Session.OpenedProcess, effectiveAddress, effectiveAddress + (UInt64)this.viewSize.Length);
                    break;
                default:
                    MemoryProtectionEnum requiredPageFlags = 0;
                    MemoryProtectionEnum excludedPageFlags = 0;
                    MemoryTypeEnum allowedTypeFlags = MemoryTypeEnum.None | MemoryTypeEnum.Private | MemoryTypeEnum.Image | MemoryTypeEnum.Mapped;

                    UInt64 startAddress = 0;
                    UInt64 endAddress = MemoryQueryer.Instance.GetMaxUsermodeAddress(SessionManager.Session.OpenedProcess);

                    this.snapshotRegions = MemoryQueryer.Instance.GetVirtualPages<SnapshotRegion>(
                        SessionManager.Session.OpenedProcess,
                        requiredPageFlags,
                        excludedPageFlags,
                        allowedTypeFlags,
                        startAddress,
                        endAddress)?.ToArray();
                    break;
            }

            if (this.activeRegion == null)
            {
                this.RefreshCurrentView();
            }

            this.RefreshUIBindings();
        }

        /// <summary>
        /// Reads all data to be shown in the memory viewer.
        /// </summary>
        private void ReadMemoryViewerData()
        {
            if (this.activeRegion == null)
            {
                return;
            }

            this.activeRegion.ReadAllMemory(SessionManager.Session.OpenedProcess);
            this.activeRegion.SetAlignment(MemoryAlignment.Alignment1, 1);

            if (!this.activeRegion.HasCurrentValues)
            {
                this.MemoryStream = null;
            }
            else
            {
                if (this.MemoryStream == null)
                {
                    this.MemoryStream = new MemoryStream(this.activeRegion.CurrentValues);
                }
                else
                {
                    try
                    {
                        this.MemoryStream.Seek(0, SeekOrigin.Begin);
                        this.MemoryStream.Write(this.activeRegion.CurrentValues, 0, (Int32)this.MemoryStream.Length);
                        this.RaisePropertyChanged(nameof(this.MemoryStream));
                    }
                    catch(Exception ex)
                    {
                    }
                }
            }
        }
        
        private void RefreshCurrentView()
        {
            this.activeRegion = this.currentPage < (this.snapshotRegions?.Length ?? 0) ? this.snapshotRegions[this.currentPage] : null;
        }

        private void RefreshUIBindings()
        {
            this.RaisePropertyChanged(nameof(this.CurrentPage));
            this.RaisePropertyChanged(nameof(this.PageCount));
            this.RaisePropertyChanged(nameof(this.CanNavigateFirst));
            this.RaisePropertyChanged(nameof(this.CanNavigatePrevious));
            this.RaisePropertyChanged(nameof(this.CanNavigateNext));
            this.RaisePropertyChanged(nameof(this.CanNavigateLast));
            this.RaisePropertyChanged(nameof(this.MemoryStream));
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
                    this.RebuildSnapshot();
                    Thread.Sleep(5000);
                }
            });
            Task.Run(() =>
            {
                while (true)
                {
                    this.ReadMemoryViewerData();
                    Thread.Sleep(50);
                }
            });
        }
    }
    //// End class
}
//// End namespace