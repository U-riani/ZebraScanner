using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ZebraSCannerTest1.UI.Converters
{
    public class ManualToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null)
                    return Colors.Transparent;

                int isManual = 0;

                // Safely normalize any type to int
                if (value is bool b)
                    isManual = b ? 1 : 0;
                else if (value is int i)
                    isManual = i;
                else if (value is long l)
                    isManual = (int)l;
                else if (value is double d)
                    isManual = d != 0 ? 1 : 0;
                else if (value is string s && int.TryParse(s, out int parsed))
                    isManual = parsed;

                // Return color based on IsManual flag
                return isManual == 1 ? Color.FromArgb("#FFD6E7") : Colors.Transparent;
            }
            catch
            {
                // Never crash on converter — always return default
                return Colors.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
