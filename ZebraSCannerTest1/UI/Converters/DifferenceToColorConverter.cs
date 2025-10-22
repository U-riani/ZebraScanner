using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ZebraSCannerTest1.UI.Converters
{
    public class DifferenceToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int diff)
            {
                if (diff < 0) return Color.FromArgb("#FFCDD2");   // light red
                if (diff == 0) return Color.FromArgb("#C8E6C9");  // light green
                if (diff > 0) return Color.FromArgb("#FFE0B2");
            }
            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
