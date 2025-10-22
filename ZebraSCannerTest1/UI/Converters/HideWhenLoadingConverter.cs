using System.Globalization;

namespace ZebraSCannerTest1.UI.Converters
{
    public class HideWhenLoadingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }
}
