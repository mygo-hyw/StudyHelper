using System;
using System.Globalization;
using System.Windows.Data;

namespace StudyHelper.Converters
{
    public class ProgressWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3) return 0.0;
            double value = System.Convert.ToDouble(values[0]);
            double max = System.Convert.ToDouble(values[1]);
            double actualWidth = System.Convert.ToDouble(values[2]);
            if (max <= 0 || actualWidth <= 0) return 0.0;
            return Math.Max(0, Math.Min(actualWidth, value / max * actualWidth));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
