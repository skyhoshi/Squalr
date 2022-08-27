﻿namespace Squalr.View
{
    using Squalr.Source.Controls;
    using Squalr.Source.Results;
    using Squalr.Source.Scanning;
    using System;
    using System.ComponentModel;
    using System.Windows;

    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ManualScanner"/> class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();

            this.ValueHexDecBoxViewModel = this.ValueHexDecBox.DataContext as HexDecBoxViewModel;
            this.ValueHexDecBoxViewModel.PropertyChanged += HexDecBoxViewModelPropertyChanged;

            ScanResultsViewModel.GetInstance().PropertyChanged += ScanResultsPropertyChanged;
        }

        private HexDecBoxViewModel ValueHexDecBoxViewModel { get; set; }

        private void ScanResultsPropertyChanged(Object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScanResultsViewModel.ActiveType))
            {
                ValueHexDecBoxViewModel.DataType = ScanResultsViewModel.GetInstance().ActiveType;
            }
        }

        private void HexDecBoxViewModelPropertyChanged(Object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ValueHexDecBoxViewModel.Text))
            {
                ManualScannerViewModel.GetInstance().UpdateActiveValueCommand.Execute(this.ValueHexDecBoxViewModel.GetValue());
            }
        }
    }
    //// End class
}
//// End namespace