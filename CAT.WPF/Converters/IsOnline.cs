namespace CAT.WPF.Converters
{
    using CAT.Model;
    using System;
    using System.Globalization;
    using System.Windows.Data;

    public class IsOnline : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "LightGreen" : "White"; 
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
