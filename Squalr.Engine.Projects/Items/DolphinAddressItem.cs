namespace Squalr.Engine.Projects.Items
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.Extensions;
    using Squalr.Engine.Memory;
    using Squalr.Engine.Processes;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.Serialization;

    /// <summary>
    /// Defines an address that can be added to the project explorer.
    /// </summary>
    [DataContract]
    public class DolphinAddressItem : AddressItem
    {
        /// <summary>
        /// The extension for this project item type.
        /// </summary>
        public new const String Extension = ".ptr";

        /// <summary>
        /// The address of this item in emulator memory.
        /// </summary>
        [DataMember]
        protected UInt64 emulatorAddress;

        /// <summary>
        /// The pointer offsets of this address item.
        /// </summary>
        [DataMember]
        protected IEnumerable<Int32> pointerOffsets;

        /// <summary>
        /// Initializes a new instance of the <see cref="AddressItem" /> class.
        /// </summary>
        public DolphinAddressItem(ProcessSession processSession) : this(processSession, 0, ScannableType.Int32, "New Address")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddressItem" /> class.
        /// </summary>
        /// <param name="baseAddress">The base address. This will be added as an offset from the resolved base identifier.</param>
        /// <param name="dataType">The data type of the value at this address.</param>
        /// <param name="description">The description of this address.</param>
        /// <param name="moduleName">The identifier for the base address of this object.</param>
        /// <param name="pointerOffsets">The pointer offsets of this address item.</param>
        /// <param name="isValueHex">A value indicating whether the value at this address should be displayed as hex.</param>
        /// <param name="value">The value at this address. If none provided, it will be figured out later. Used here to allow immediate view updates upon creation.</param>
        public DolphinAddressItem(
            ProcessSession processSession,
            UInt64 baseAddress,
            Type dataType,
            String description = "New Address",
            IEnumerable<Int32> pointerOffsets = null,
            Boolean isValueHex = false,
            Object value = null)
            : base(processSession, dataType, description, isValueHex, value)
        {
            // Bypass setters to avoid running setter code
            this.emulatorAddress = baseAddress;
            this.pointerOffsets = pointerOffsets;
        }

        /// <summary>
        /// Gets or sets the emulator address of this object.
        /// </summary>
        public virtual UInt64 EmulatorAddress
        {
            get
            {
                return this.emulatorAddress;
            }

            set
            {
                if (this.emulatorAddress == value)
                {
                    return;
                }

                this.CalculatedAddress = value;
                this.emulatorAddress = value;
                this.RaisePropertyChanged(nameof(this.EmulatorAddress));
                this.RaisePropertyChanged(nameof(this.AddressSpecifier));
                this.Save();
            }
        }

        /// <summary>
        /// Gets or sets the pointer offsets of this address item.
        /// </summary>
        public virtual IEnumerable<Int32> PointerOffsets
        {
            get
            {
                return this.pointerOffsets;
            }

            set
            {
                if (this.pointerOffsets != null && this.pointerOffsets.SequenceEqual(value))
                {
                    return;
                }

                this.pointerOffsets = value;
                this.RaisePropertyChanged(nameof(this.PointerOffsets));
                this.RaisePropertyChanged(nameof(this.IsPointer));
                this.RaisePropertyChanged(nameof(this.AddressSpecifier));
                this.Save();
            }
        }

        /// <summary>
        /// Gets the address specifier for this address. If a static address, this is 'ModuleName + offset', otherwise this is an address string.
        /// </summary>
        [Browsable(false)]
        public String AddressSpecifier
        {
            get
            {
                if (this.IsPointer)
                {
                    return Conversions.ToHex(this.CalculatedAddress);
                }
                else
                {
                    return Conversions.ToHex(this.EmulatorAddress);
                }
            }
        }

        /// <summary>
        /// Gets a value indicating if this object is a true pointer and not just an address.
        /// </summary>
        [Browsable(false)]
        public Boolean IsPointer
        {
            get
            {
                return !this.PointerOffsets.IsNullOrEmpty();
            }
        }

        /// <summary>
        /// Gets the extension for this project item.
        /// </summary>
        public override String GetExtension()
        {
            return PointerItem.Extension;
        }

        /// <summary>
        /// Resolves the address of an address, pointer, or managed object.
        /// </summary>
        /// <returns>The base address of this object.</returns>
        protected override UInt64 ResolveAddress()
        {
            UInt64 pointer = MemoryQueryer.Instance.ResolveEmulatorAddress(processSession?.OpenedProcess, this.EmulatorAddress, EmulatorType.Dolphin);

            pointer = pointer.Add(this.EmulatorAddress);

            if (this.PointerOffsets == null || this.PointerOffsets.Count() == 0)
            {
                return pointer;
            }

            foreach (Int32 offset in this.PointerOffsets)
            {
                bool successReading = false;

                if (processSession?.OpenedProcess?.Is32Bit() ?? false)
                {
                    pointer = MemoryReader.Instance.Read<Int32>(processSession?.OpenedProcess, pointer, out successReading).ToUInt64();
                }
                else
                {
                    pointer = MemoryReader.Instance.Read<UInt64>(processSession?.OpenedProcess, pointer, out successReading);
                }

                if (pointer == 0 || !successReading)
                {
                    return 0;
                }

                pointer = pointer.Add(offset);
            }

            // TODO: Unresolve?
            return pointer;
        }
    }
    //// End class
}
//// End namespace
