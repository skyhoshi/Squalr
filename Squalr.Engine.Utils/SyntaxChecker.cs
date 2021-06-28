namespace Squalr.Engine.Common
{
    using Squalr.Engine.Common;
    using System;
    using System.Globalization;

    /// <summary>
    /// A static class used to check syntax for various values and types.
    /// </summary>
    public static class SyntaxChecker
    {
        /// <summary>
        /// Checks if the provided string is a valid address.
        /// </summary>
        /// <param name="address">The address as a hex string.</param>
        /// <param name="mustBe32Bit">Whether or not the address must strictly be containable in 32 bits.</param>
        /// <returns>A boolean indicating if the address is parseable.</returns>
        public static Boolean CanParseAddress(String address, Boolean mustBe32Bit = false)
        {
            if (address == null)
            {
                return false;
            }

            // Remove 0x hex specifier
            if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                address = address.Substring(2);
            }

            // Remove trailing 0s
            while (address.StartsWith("0") && address.Length > 1)
            {
                address = address.Substring(1);
            }

            if (mustBe32Bit)
            {
                return IsUInt32(address, true);
            }
            else
            {
                return IsUInt64(address, true);
            }
        }

        /// <summary>
        /// Determines if a value of the given type can be parsed from the given string.
        /// </summary>
        /// <param name="dataType">The type of the given value.</param>
        /// <param name="value">The value to be parsed.</param>
        /// <returns>A boolean indicating if the value is parseable.</returns>
        public static Boolean CanParseValue(ScannableType dataType, String value)
        {
            if (dataType == (ScannableType)null)
            {
                return false;
            }

            switch (dataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return SyntaxChecker.IsByte(value);
                case ScannableType type when type == ScannableType.SByte:
                    return SyntaxChecker.IsSByte(value);
                case ScannableType type when type == ScannableType.Int16:
                    return SyntaxChecker.IsInt16(value);
                case ScannableType type when type == ScannableType.Int32:
                    return SyntaxChecker.IsInt32(value);
                case ScannableType type when type == ScannableType.Int64:
                    return SyntaxChecker.IsInt64(value);
                case ScannableType type when type == ScannableType.UInt16:
                    return SyntaxChecker.IsUInt16(value);
                case ScannableType type when type == ScannableType.UInt32:
                    return SyntaxChecker.IsUInt32(value);
                case ScannableType type when type == ScannableType.UInt64:
                    return SyntaxChecker.IsUInt64(value);
                case ScannableType type when type == ScannableType.Single:
                    return SyntaxChecker.IsSingle(value);
                case ScannableType type when type == ScannableType.Double:
                    return SyntaxChecker.IsDouble(value);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines if a hex value can be parsed from the given string.
        /// </summary>
        /// <param name="dataType">The type of the given value.</param>
        /// <param name="value">The value to be parsed.</param>
        /// <returns>A boolean indicating if the value is parseable as hex.</returns>
        public static Boolean CanParseHex(ScannableType dataType, String value)
        {
            if (value == null)
            {
                return false;
            }

            // Remove 0x hex specifier
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(2);
            }

            // Remove trailing 0s
            while (value.StartsWith("0") && value.Length > 1)
            {
                value = value.Substring(1);
            }

            // Remove negative sign from signed integer types, as TryParse methods do not handle negative hex values
            switch (dataType)
            {
                case ScannableType type when type == ScannableType.Byte || type == ScannableType.Int16 || type == ScannableType.Int32 || type == ScannableType.Int64:
                    if (value.StartsWith("-"))
                    {
                        value = value.Substring(1);
                    }

                    break;
                default:
                    break;
            }

            switch (dataType)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return IsByte(value, true);
                case ScannableType type when type == ScannableType.SByte:
                    return IsSByte(value, true);
                case ScannableType type when type == ScannableType.Int16:
                    return IsInt16(value, true);
                case ScannableType type when type == ScannableType.Int32:
                    return IsInt32(value, true);
                case ScannableType type when type == ScannableType.Int64:
                    return IsInt64(value, true);
                case ScannableType type when type == ScannableType.UInt16:
                    return IsUInt16(value, true);
                case ScannableType type when type == ScannableType.UInt32:
                    return IsUInt32(value, true);
                case ScannableType type when type == ScannableType.UInt64:
                    return IsUInt64(value, true);
                case ScannableType type when type == ScannableType.Single:
                    return IsSingle(value, true);
                case ScannableType type when type == ScannableType.Double:
                    return IsDouble(value, true);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines if the given string can be parsed as a byte.
        /// </summary>
        /// <param name="value">The value as a string.</param>
        /// <param name="isHex">Whether or not the value is encoded in hex.</param>
        /// <returns>A boolean indicating if the value could be parsed.</returns>
        private static Boolean IsByte(String value, Boolean isHex = false)
        {
            if (isHex)
            {
                return Byte.TryParse(value, NumberStyles.HexNumber, null, out _);
            }
            else
            {
                return Byte.TryParse(value, out _);
            }
        }

        /// <summary>
        /// Determines if the given string can be parsed as a signed byte.
        /// </summary>
        /// <param name="value">The value as a string.</param>
        /// <param name="isHex">Whether or not the value is encoded in hex.</param>
        /// <returns>A boolean indicating if the value could be parsed.</returns>
        private static Boolean IsSByte(String value, Boolean isHex = false)
        {
            if (isHex)
            {
                return SByte.TryParse(value, NumberStyles.HexNumber, null, out _);
            }
            else
            {
                return SByte.TryParse(value, out _);
            }
        }

        /// <summary>
        /// Determines if the given string can be parsed as a 16 bit integer.
        /// </summary>
        /// <param name="value">The value as a string.</param>
        /// <param name="isHex">Whether or not the value is encoded in hex.</param>
        /// <returns>A boolean indicating if the value could be parsed.</returns>
        private static Boolean IsInt16(String value, Boolean isHex = false)
        {
            if (isHex)
            {
                return Int16.TryParse(value, NumberStyles.HexNumber, null, out _);
            }
            else
            {
                return Int16.TryParse(value, out _);
            }
        }

        /// <summary>
        /// Determines if the given string can be parsed as a 16 bit signed integer.
        /// </summary>
        /// <param name="value">The value as a string.</param>
        /// <param name="isHex">Whether or not the value is encoded in hex.</param>
        /// <returns>A boolean indicating if the value could be parsed.</returns>
        private static Boolean IsUInt16(String value, Boolean isHex = false)
        {
            if (isHex)
            {
                return UInt16.TryParse(value, NumberStyles.HexNumber, null, out _);
            }
            else
            {
                return UInt16.TryParse(value, out _);
            }
        }

        /// <summary>
        /// Determines if the given string can be parsed as a 32 bit integer.
        /// </summary>
        /// <param name="value">The value as a string.</param>
        /// <param name="isHex">Whether or not the value is encoded in hex.</param>
        /// <returns>A boolean indicating if the value could be parsed.</returns>
        private static Boolean IsInt32(String value, Boolean isHex = false)
        {
            if (isHex)
            {
                return Int32.TryParse(value, NumberStyles.HexNumber, null, out _);
            }
            else
            {
                return Int32.TryParse(value, out _);
            }
        }

        /// <summary>
        /// Determines if the given string can be parsed as a 32 bit signed integer.
        /// </summary>
        /// <param name="value">The value as a string.</param>
        /// <param name="isHex">Whether or not the value is encoded in hex.</param>
        /// <returns>A boolean indicating if the value could be parsed.</returns>
        private static Boolean IsUInt32(String value, Boolean isHex = false)
        {
            if (isHex)
            {
                return UInt32.TryParse(value, NumberStyles.HexNumber, null, out _);
            }
            else
            {
                return UInt32.TryParse(value, out _);
            }
        }

        /// <summary>
        /// Determines if the given string can be parsed as a 64 bit integer.
        /// </summary>
        /// <param name="value">The value as a string.</param>
        /// <param name="isHex">Whether or not the value is encoded in hex.</param>
        /// <returns>A boolean indicating if the value could be parsed.</returns>
        private static Boolean IsInt64(String value, Boolean isHex = false)
        {
            if (isHex)
            {
                return Int64.TryParse(value, NumberStyles.HexNumber, null, out _);
            }
            else
            {
                return Int64.TryParse(value, out _);
            }
        }

        /// <summary>
        /// Determines if the given string can be parsed as a 64 bit signed integer.
        /// </summary>
        /// <param name="value">The value as a string.</param>
        /// <param name="isHex">Whether or not the value is encoded in hex.</param>
        /// <returns>A boolean indicating if the value could be parsed.</returns>
        private static Boolean IsUInt64(String value, Boolean isHex = false)
        {
            if (isHex)
            {
                return UInt64.TryParse(value, NumberStyles.HexNumber, null, out _);
            }
            else
            {
                return UInt64.TryParse(value, out _);
            }
        }

        /// <summary>
        /// Determines if the given string can be parsed as a single precision floating point number.
        /// </summary>
        /// <param name="value">The value as a string.</param>
        /// <param name="isHex">Whether or not the value is encoded in hex.</param>
        /// <returns>A boolean indicating if the value could be parsed.</returns>
        private static Boolean IsSingle(String value, Boolean isHex = false)
        {
            if (isHex && IsUInt32(value, isHex))
            {
                return Single.TryParse(Conversions.ParseHexStringAsPrimitiveString(ScannableType.Single, value), out _);
            }
            else
            {
                return Single.TryParse(value.EndsWith("f") ? value.Remove(value.LastIndexOf("f")) : value, out _);
            }
        }

        /// <summary>
        /// Determines if the given string can be parsed as a double precision floating point number.
        /// </summary>
        /// <param name="value">The value as a string.</param>
        /// <param name="isHex">Whether or not the value is encoded in hex.</param>
        /// <returns>A boolean indicating if the value could be parsed.</returns>
        private static Boolean IsDouble(String value, Boolean isHex = false)
        {
            if (isHex && IsUInt64(value, isHex))
            {
                return Double.TryParse(Conversions.ParseHexStringAsPrimitiveString(ScannableType.Double, value), out _);
            }
            else
            {
                return Double.TryParse(value, out _);
            }
        }
    }
    //// End class
}
//// End namespace