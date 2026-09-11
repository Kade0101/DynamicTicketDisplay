using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RaffleDisplayApplication
{
    public class TypeToVisibilityConverter : IValueConverter
    {
        public Type TargetType { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && TargetType != null && value.GetType() == TargetType)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}