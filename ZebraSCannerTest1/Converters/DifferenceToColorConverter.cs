using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ZebraSCannerTest1.Converters
{
    public class DifferenceToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int diff)
            {
                if (diff < 0) return Colors.Orange;   // shortage
                if (diff == 0) return Colors.Green;   // exact match
                if (diff > 0) return Colors.LightGray; // overstock
            }
            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
