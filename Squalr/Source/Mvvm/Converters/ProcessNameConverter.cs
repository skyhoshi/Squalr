namespace Squalr.Source.Mvvm.Converters
{
    using Squalr.Engine.Processes;
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Windows.Data;

    /// <summary>
    /// Converts a process to a name to be displayed. Allows displaying null processes as a "detach" icon.
    /// </summary>
    public class ProcessNameConverter : IValueConverter
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
            if ((value as Process) == DetachProcess.Instance)
            {
                return "== Detach from current process ==";
            }

            return (value as Process)?.ProcessName;
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