// QuantityComparisonToColorConverter.cs
using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ZebraSCannerTest1.UI.Converters
{
    public class QuantityComparisonToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter is used with MultiBinding - value will be null here
            // Actual logic is in IMultiValueConverter version below
            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ────────────────────────────────────────────────
    // Use this IMultiValueConverter version instead (better)
    // Replace the above with this if you prefer multi-binding

    public class ScannedVsInitialColorMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not int scanned || values[1] is not int initial)
                return Colors.Transparent;

            if (scanned < initial) return Color.FromArgb("#FFCDD2"); // light red
            if (scanned == initial) return Color.FromArgb("#C8E6C9"); // light green
            if (scanned > initial) return Color.FromArgb("#FFE0B2"); // light yellow

            return Colors.Transparent;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}