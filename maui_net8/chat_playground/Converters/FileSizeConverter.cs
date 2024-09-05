using ChatPlayground.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatPlayground.Converters
{
    class FileSizeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null)
            {
                Type type = value.GetType();

                if (value is long bytes)
                {
                    return FormatFileSize(bytes);
                }
            }

            return string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static string FormatFileSize(long bytes)
        {
            var unit = 1024;

            if (bytes < unit)
            {
                return $"{bytes} B";
            }

            var exp = (int)(Math.Log(bytes) / Math.Log(unit));

            return $"{bytes / Math.Pow(unit, exp):F2} {("KMGTPE")[exp - 1]}B";
        }
    }
}
