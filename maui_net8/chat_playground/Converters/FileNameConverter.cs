using System.Globalization;

namespace ChatPlayground.Converters;

class FileNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value != null)
        {
            if (value is Uri uri && uri.IsFile)
            {
                return Path.GetFileName(uri.LocalPath);
            }
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
