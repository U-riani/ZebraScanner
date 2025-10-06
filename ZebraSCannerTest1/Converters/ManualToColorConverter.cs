using System.Globalization;
using Microsoft.Maui.Controls;

namespace ZebraSCannerTest1.Converters
{
    public class ManualToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Colors.Transparent; // normal scan
            if (value is int i && i == 1)
                return Color.FromArgb("#FFD6E7"); // light pink
            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
