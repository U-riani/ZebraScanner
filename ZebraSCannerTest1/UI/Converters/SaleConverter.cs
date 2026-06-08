using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.UI.Converters
{
    public class SaleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string saleType = value?.ToString()?.Trim();

            return saleType switch
            {
                "1" => Color.FromArgb("#C8E6C9"), // Light green
                "2" => Color.FromArgb("#FFE0B2"), 
                _ => Colors.Transparent
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
