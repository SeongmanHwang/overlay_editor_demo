using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Utils
{
    public class FileNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                return Path.GetFileName(path);
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MarkedCountConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is List<MarkingResult> results)
            {
                return results.Count(r => r.IsMarked);
            }
            return 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class UnmarkedCountConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is List<MarkingResult> results)
            {
                return results.Count(r => !r.IsMarked);
            }
            return 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BarcodeSuccessCountConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is List<BarcodeResult> results)
            {
                return results.Count(r => r.Success);
            }
            return 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BarcodeFailureCountConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is List<BarcodeResult> results)
            {
                return results.Count(r => !r.Success);
            }
            return 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    public class StringEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return parameter?.ToString() ?? string.Empty;
            }
            return Binding.DoNothing;
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // null이면 Visible, null이 아니면 Collapsed (정렬 정보가 없을 때 메시지 표시)
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBoolToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    public class BoolToYesNoConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "예" : "아니오";
            }
            return "아니오";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                return strValue == "예";
            }
            return false;
        }
    }

    public class DoubleToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double doubleValue && parameter is string thresholdStr)
            {
                if (double.TryParse(thresholdStr, NumberStyles.Float, culture, out double threshold))
                {
                    return doubleValue >= threshold;
                }
            }
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DateTimeDisplayConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            DateTime? dateTime = null;
            
            if (value is DateTime dt)
            {
                dateTime = dt;
            }
            else if (value != null && value.GetType() == typeof(DateTime?))
            {
                dateTime = (DateTime?)value;
            }

            if (dateTime.HasValue)
            {
                return $"{dateTime.Value.Year}년 {dateTime.Value.Month}월 {dateTime.Value.Day}일 {dateTime.Value.Hour}시 {dateTime.Value.Minute:D2}분";
            }
            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FileSizeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is long size)
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = size;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EnumToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DataUsageItem.ItemType itemType && parameter is string param)
            {
                if (param == "JsonFile" && itemType == DataUsageItem.ItemType.JsonFile)
                    return Visibility.Visible;
                if (param == "Folder" && itemType == DataUsageItem.ItemType.Folder)
                    return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x42, 0x42, 0x42));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ItemColorConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x42, 0x42, 0x42));

            var isMostRecent = values[0] is bool && (bool)values[0];
            var itemName = values[1] as string;

            // 로그는 항상 노란색
            if (itemName == "로그")
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
            }

            // 가장 최신 항목은 붉은색
            if (isMostRecent)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }

            // 기본 색상
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x42, 0x42, 0x42));
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}



