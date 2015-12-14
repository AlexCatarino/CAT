namespace CAT.WPF.Converters
{
    using System;
    using System.Globalization;
    using System.Windows.Data;
    public class IsBuyOrSell : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value > 0 ? "C" : "V";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
