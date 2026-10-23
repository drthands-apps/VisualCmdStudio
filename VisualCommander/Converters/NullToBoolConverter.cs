using System;
using System.Globalization;
using System.Windows.Data;

namespace VisualCommander.Converters
{

    /// <summary>
    /// Convierte un valor a bool: true si no es null, false si es null.
    /// Útil para DataTriggers que necesitan comprobar si un binding
    /// tiene valor.
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value != null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}