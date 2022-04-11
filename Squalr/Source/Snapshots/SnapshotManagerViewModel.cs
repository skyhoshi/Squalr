using CommunityToolkit.Mvvm.Input;

namespace Squalr.Source.Snapshots
{
    
    using Squalr.Engine.Scanning.Snapshots;
    using Squalr.Source.Docking;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Windows.Input;

    /// <summary>
    /// View model for the Snapshot Manager.
    /// </summary>
    public class SnapshotManagerViewModel : ToolViewModel
    {
        /// <summary>
        /// Singleton instance of the <see cref="SnapshotManagerViewModel"/> class.
        /// </summary>
        private static Lazy<SnapshotManagerViewModel> snapshotManagerViewModelInstance = new Lazy<SnapshotManagerViewModel>(
                () => { return new SnapshotManagerViewModel(); },
                LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Prevents a default instance of the <see cref="SnapshotManagerViewModel"/> class from being created.
        /// </summary>
        private SnapshotManagerViewModel() : base("Snapshot Manager")
        {
            // Note: Not async to avoid updates slower than the perception threshold
            this.ClearSnapshotsCommand = new RelayCommand(() => SessionManager.Session?.SnapshotManager?.ClearSnapshots(), () => true);
            this.UndoSnapshotCommand = new RelayCommand(() => SessionManager.Session?.SnapshotManager?.UndoSnapshot(), () => true);
            this.RedoSnapshotCommand = new RelayCommand(() => SessionManager.Session?.SnapshotManager?.RedoSnapshot(), () => true);

            SessionManager.Session.SnapshotManager.OnSnapshotsUpdatedEvent += SnapshotManagerOnSnapshotsUpdatedEvent;

            DockingViewModel.GetInstance().RegisterViewModel(this);
        }

        private void SnapshotManagerOnSnapshotsUpdatedEvent(SnapshotManager snapshotManager)
        {
            this.OnPropertyChanged(nameof(this.Snapshots));
            this.OnPropertyChanged(nameof(this.DeletedSnapshots));
        }

        /// <summary>
        /// Gets a command to start a new scan.
        /// </summary>
        public ICommand ClearSnapshotsCommand { get; private set; }

        /// <summary>
        /// Gets a command to undo the last scan.
        /// </summary>
        public ICommand UndoSnapshotCommand { get; private set; }

        /// <summary>
        /// Gets a command to redo the last scan.
        /// </summary>
        public ICommand RedoSnapshotCommand { get; private set; }

        /// <summary>
        /// Gets the enumeration of snapshots in the snapshot manager.
        /// </summary>
        public IEnumerable<Snapshot> Snapshots
        {
            get
            {
                return SessionManager.Session?.SnapshotManager?.Snapshots;
            }
        }

        /// <summary>
        /// Gets the enumeration of snapshots in the snapshot manager.
        /// </summary>
        public IEnumerable<Snapshot> DeletedSnapshots
        {
            get
            {
                return SessionManager.Session?.SnapshotManager?.DeletedSnapshots;
            }
        }

        /// <summary>
        /// Gets a singleton instance of the <see cref="SnapshotManagerViewModel"/> class.
        /// </summary>
        /// <returns>A singleton instance of the class.</returns>
        public static SnapshotManagerViewModel GetInstance()
        {
            return SnapshotManagerViewModel.snapshotManagerViewModelInstance.Value;
        }
    }
    //// End class
}
//// End namespace