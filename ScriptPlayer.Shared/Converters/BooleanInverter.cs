﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace ScriptPlayer.Shared.Converters
{
    public class BooleanInverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool val)
                return !val;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool val)
                return !val;
            return true;
        }
    }
}
