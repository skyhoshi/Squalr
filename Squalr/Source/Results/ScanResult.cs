﻿namespace Squalr.Source.Results
{
    using Squalr.Engine.Projects.Items;
    using Squalr.Source.ProjectExplorer.ProjectItems;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// A scan result object that can be displayed to the user and added to the project explorer.
    /// </summary>
    public class ScanResult : INotifyPropertyChanged
    {
        /// <summary>
        /// The previous value of the scan result.
        /// </summary>
        private Object previousValue;

        /// <summary>
        /// The label of the scan result.
        /// </summary>
        private String label;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanResult" /> class.
        /// </summary>
        /// <param name="pointerItem">The inner pointer item.</param>
        /// <param name="dataType">The data type of the value at this address.</param>
        /// <param name="value">The initial value of this result.</param>
        /// <param name="previousValue">The previous scan value.</param>
        /// <param name="label">The label of this result.</param>
        public ScanResult(PointerItemView pointerItem, Object previousValue, String label)
        {
            this.PointerItem = pointerItem;
            this.PreviousValue = previousValue;
            this.Label = label;
            
            this.PointerItem.PropertyChanged += PointerItemChanged;
        }

        /// <summary>
        /// Gets the pointer item this scan result contains.
        /// </summary>
        public PointerItemView PointerItem { get; private set; }

        /// <summary>
        /// Gets or sets the display value of the scan result.
        /// </summary>
        [Browsable(false)]
        public Object DisplayValue
        {
            get
            {
                return this.PointerItem.DisplayValue;
            }

            set
            {
                this.PointerItem.DisplayValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the address specifier of the scan result.
        /// </summary>
        [Browsable(false)]
        public String AddressSpecifier
        {
            get
            {
                return this.PointerItem.AddressSpecifier;
            }
        }

        /// <summary>
        /// Gets or sets the previous value of the scan result.
        /// </summary>
        [Browsable(false)]
        public Object PreviousValue
        {
            get
            {
                return this.previousValue;
            }

            set
            {
                this.previousValue = value;
                this.OnPropertyChanged(nameof(this.PreviousValue));
            }
        }

        /// <summary>
        /// Gets or sets the label of the scan result.
        /// </summary>
        [Browsable(false)]
        public String Label
        {
            get
            {
                return this.label;
            }

            set
            {
                this.label = value;
                this.OnPropertyChanged(nameof(this.Label));
            }
        }

        /// <summary>
        /// Occurs after a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Event fired when a property in the pointer item changes.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="e">The event args.</param>
        public void PointerItemChanged(Object sender, PropertyChangedEventArgs e)
        {
            this.OnPropertyChanged(nameof(this.DisplayValue));
            this.OnPropertyChanged(nameof(this.AddressSpecifier));
        }

        /// <summary>
        /// Indicates that a given property in this project item has changed.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        protected void OnPropertyChanged(String propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    //// End class
}
//// End namespace