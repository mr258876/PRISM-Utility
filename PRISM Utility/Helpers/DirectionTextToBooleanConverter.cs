using Microsoft.UI.Xaml.Data;

namespace PRISM_Utility.Helpers;

public class DirectionTextToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string directionText)
        {
            return string.Equals(directionText, "Reverse", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
