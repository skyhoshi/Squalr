namespace Squalr.Engine.Projects.Items
{
    using Squalr.Engine.Common;
    using Squalr.Engine.Common.DataTypes;
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
        public new const String Extension = ".dol";

        /// <summary>
        /// Initializes a new instance of the <see cref="DolphinAddressItem" /> class.
        /// </summary>
        public DolphinAddressItem(ProcessSession processSession) : this(processSession, DataTypeBase.Int32, "New Address")
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
            DataTypeBase dataType,
            String description = "New Address",
            Boolean isValueHex = false,
            Object value = null)
            : base(processSession, dataType, description, isValueHex, value)
        {
        }

        /// <summary>
        /// Gets the address specifier for this address. If a static address, this is 'ModuleName + offset', otherwise this is an address string.
        /// </summary>
        [Browsable(false)]
        public String AddressSpecifier
        {
            get
            {
                return String.Empty;
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
            return 0;
        }
    }
    //// End class
}
//// End namespace