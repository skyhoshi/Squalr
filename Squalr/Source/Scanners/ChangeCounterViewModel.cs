﻿using CommunityToolkit.Mvvm.Input;

namespace Squalr.Source.Scanning
{
    
    using Squalr.Engine.Scanning.Scanners;
    using Squalr.Source.Docking;
    using Squalr.View;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    /// <summary>
    /// View model for the Change Counter.
    /// </summary>
    public class ChangeCounterViewModel : ToolViewModel
    {
        /// <summary>
        /// Singleton instance of the <see cref="ChangeCounterViewModel" /> class.
        /// </summary>
        private static Lazy<ChangeCounterViewModel> changeCounterViewModelInstance = new Lazy<ChangeCounterViewModel>(
                () => { return new ChangeCounterViewModel(); },
                LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Prevents a default instance of the <see cref="ChangeCounterViewModel" /> class from being created.
        /// </summary>
        private ChangeCounterViewModel() : base("Change Counter")
        {
            this.StartScanCommand = new RelayCommand(() => Task.Run(() => this.StartScan()), () => true);
            this.StopScanCommand = new RelayCommand(() => Task.Run(() => this.StopScan()), () => true);
            this.ChangeCounterScan = null; // new ChangeCounter(this.ScanCountUpdated);

            DockingViewModel.GetInstance().RegisterViewModel(this);
        }

        public ICommand StartScanCommand { get; private set; }

        public ICommand StopScanCommand { get; private set; }

        public Int32 ScanCount
        {
            get
            {
                throw new NotImplementedException();
               //// return this.ChangeCounterScan.ScanCount;
            }
        }

        private ChangeCounter ChangeCounterScan { get; set; }

        /// <summary>
        /// Gets a singleton instance of the <see cref="ChangeCounterViewModel"/> class.
        /// </summary>
        /// <returns>A singleton instance of the class.</returns>
        public static ChangeCounterViewModel GetInstance()
        {
            return ChangeCounterViewModel.changeCounterViewModelInstance.Value;
        }

        private void ScanCountUpdated()
        {
            this.OnPropertyChanged(nameof(this.ScanCount));
        }

        private void StartScan()
        {
            //// this.ChangeCounterScan.Start();
        }

        private void StopScan()
        {
            throw new NotImplementedException();
            ////this.ChangeCounterScan.Stop();
        }
    }
    //// End class
}
//// End namespace