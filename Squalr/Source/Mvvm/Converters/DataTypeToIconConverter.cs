namespace Squalr.Source.Mvvm.Converters
{
    using Squalr.Content;
    using Squalr.Engine.Common;
    using System;
    using System.Globalization;
    using System.Windows.Data;

    /// <summary>
    /// Converts ScannableTypes to an icon format readily usable by the view.
    /// </summary>
    public class DataTypeToIconConverter : IValueConverter
    {
        /// <summary>
        /// Converts an Icon to a BitmapSource.
        /// </summary>
        /// <param name="value">Value to be converted.</param>
        /// <param name="targetType">Type to convert to.</param>
        /// <param name="parameter">Optional conversion parameter.</param>
        /// <param name="culture">Globalization info.</param>
        /// <returns>Object with type of BitmapSource. If conversion cannot take place, returns null.</returns>
        public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            if (parameter != null)
            {
                value = parameter;
            }

            if (value is Type)
            {
                value = new ScannableType(value as Type);
            }

            switch (value)
            {
                case ScannableType type when type == ScannableType.Byte:
                    return Images.PurpleBlocks1;
                case ScannableType type when type == ScannableType.Char:
                    return Images.PurpleBlocks1;
                case ScannableType type when type == ScannableType.SByte:
                    return Images.BlueBlocks1;
                case ScannableType typeBE when typeBE == ScannableType.Int16BE:
                case ScannableType type when type == ScannableType.Int16:
                    return Images.BlueBlocks2;
                case ScannableType typeBE when typeBE == ScannableType.Int32BE:
                case ScannableType type when type == ScannableType.Int32:
                    return Images.BlueBlocks4;
                case ScannableType typeBE when typeBE == ScannableType.Int64BE:
                case ScannableType type when type == ScannableType.Int64:
                    return Images.BlueBlocks8;
                case ScannableType typeBE when typeBE == ScannableType.UInt16BE:
                case ScannableType type when type == ScannableType.UInt16:
                    return Images.PurpleBlocks2;
                case ScannableType typeBE when typeBE == ScannableType.UInt32BE:
                case ScannableType type when type == ScannableType.UInt32:
                    return Images.PurpleBlocks4;
                case ScannableType typeBE when typeBE == ScannableType.UInt64BE:
                case ScannableType type when type == ScannableType.UInt64:
                    return Images.PurpleBlocks8;
                case ScannableType typeBE when typeBE == ScannableType.SingleBE:
                case ScannableType type when type == ScannableType.Single:
                    return Images.OrangeBlocks4;
                case ScannableType typeBE when typeBE == ScannableType.DoubleBE:
                case ScannableType type when type == ScannableType.Double:
                    return Images.OrangeBlocks8;
                case ScannableType type when type == ScannableType.ByteArray:
                    return Images.BlueBlocksArray;
                case ScannableType type when type == ScannableType.String:
                    return Images.LetterS;
                case ScannableType type when type == ScannableType.IntPtr:
                    return !Environment.Is64BitProcess ? Images.BlueBlocks4 : Images.BlueBlocks8;
                case ScannableType type when type == ScannableType.UIntPtr:
                    return !Environment.Is64BitProcess ? Images.PurpleBlocks4 : Images.PurpleBlocks8;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Not used or implemented.
        /// </summary>
        /// <param name="value">Value to be converted.</param>
        /// <param name="targetType">Type to convert to.</param>
        /// <param name="parameter">Optional conversion parameter.</param>
        /// <param name="culture">Globalization info.</param>
        /// <returns>Throws see <see cref="NotImplementedException" />.</returns>
        public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    //// End class
}
//// End namespace