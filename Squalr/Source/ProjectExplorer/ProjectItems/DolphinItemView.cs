namespace Squalr.Source.ProjectExplorer.ProjectItems
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Projects.Items;
    using Squalr.Source.Controls;
    using Squalr.Source.Editors.OffsetEditor;
    using Squalr.Source.Editors.ValueEditor;
    using Squalr.Source.Utils.TypeConverters;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing.Design;

    /// <summary>
    /// Decorates the base project item class with annotations for use in the view.
    /// </summary>
    public class DolphinItemView : ProjectItemView
    {
        private DolphinAddressItem dolphinAddressItem;

        public DolphinItemView(DolphinAddressItem pointerItem)
        {
            this.DolphinAddressItem = pointerItem;
            this.DolphinAddressItem.PropertyChanged += PointerItemPropertyChanged;
        }

        ~DolphinItemView()
        {
            this.DolphinAddressItem.PropertyChanged -= PointerItemPropertyChanged;
        }

        private void PointerItemPropertyChanged(Object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(PointerItem.AddressValue):
                    this.RaisePropertyChanged(nameof(this.DisplayValue));
                    break;
                case nameof(PointerItem.DataType):
                    this.RaisePropertyChanged(nameof(this.DataType));
                    break;
            }
        }

        [Browsable(false)]
        private DolphinAddressItem DolphinAddressItem
        {
            get
            {
                return this.dolphinAddressItem;
            }

            set
            {
                this.dolphinAddressItem = value;
                this.ProjectItem = value;
                this.RaisePropertyChanged(nameof(this.DolphinAddressItem));
            }
        }

        /// <summary>
        /// Gets or sets the description for this object.
        /// </summary>
        [Browsable(false)]
        public String AddressSpecifier
        {
            get
            {
                return this.DolphinAddressItem.AddressSpecifier;
            }
        }

        /// <summary>
        /// Gets or sets the description for this object.
        /// </summary>
        [Browsable(true)]
        [SortedCategory(SortedCategory.CategoryType.Common), DisplayName("Name"), Description("The name of this pointer")]
        public String Name
        {
            get
            {
                return this.DolphinAddressItem.Name;
            }

            set
            {
                this.DolphinAddressItem.Name = value;
                this.RaisePropertyChanged(nameof(this.Name));
            }
        }

        /// <summary>
        /// Gets or sets the identifier for the base address of this object.
        /// </summary>
        [Browsable(true)]
        [TypeConverter(typeof(DataTypeConverter))]
        [RefreshProperties(RefreshProperties.All)]
        [SortedCategory(SortedCategory.CategoryType.Common), DisplayName("Data Type"), Description("The data type of this address")]
        public ScannableType DataType
        {
            get
            {
                return this.DolphinAddressItem.DataType;
            }

            set
            {
                this.DolphinAddressItem.DataType = value;
                this.RaisePropertyChanged(nameof(this.DataType));
            }
        }

        /// <summary>
        /// Gets or sets the base address of this object.
        /// </summary>
        [Browsable(true)]
        [RefreshProperties(RefreshProperties.All)]
        [TypeConverter(typeof(AddressConverter))]
        [SortedCategory(SortedCategory.CategoryType.Advanced), DisplayName("Emulator Address"), Description("The base address in emulator address space.")]
        public UInt64 EmulatorAddress
        {
            get
            {
                return this.DolphinAddressItem.EmulatorAddress;
            }

            set
            {
                this.DolphinAddressItem.EmulatorAddress = value;
                this.RaisePropertyChanged(nameof(this.EmulatorAddress));
            }
        }

        /// <summary>
        /// Gets or sets the pointer offsets of this address item.
        /// </summary>
        [Browsable(true)]
        [RefreshProperties(RefreshProperties.All)]
        [TypeConverter(typeof(OffsetConverter))]
        [Editor(typeof(OffsetEditorModel), typeof(UITypeEditor))]
        [SortedCategory(SortedCategory.CategoryType.Advanced), DisplayName("Pointer Offsets"), Description("The pointer offsets used to calculate the final address")]
        public IEnumerable<Int32> PointerOffsets
        {
            get
            {
                return this.DolphinAddressItem.PointerOffsets;
            }

            set
            {
                this.DolphinAddressItem.PointerOffsets = value;
                this.RaisePropertyChanged(nameof(this.PointerOffsets));
            }
        }

        [Browsable(true)]
        [RefreshProperties(RefreshProperties.All)]
        [Editor(typeof(ValueEditorModel), typeof(UITypeEditor))]
        [SortedCategory(SortedCategory.CategoryType.Common), DisplayName("Value"), Description("The value at the resolved address")]
        public override Object DisplayValue
        {
            get
            {
                return this.DolphinAddressItem.AddressValue;
            }

            set
            {
                this.DolphinAddressItem.AddressValue = value;
                this.RaisePropertyChanged(nameof(this.DisplayValue));
            }
        }
    }
    //// End class
}
//// End namespace