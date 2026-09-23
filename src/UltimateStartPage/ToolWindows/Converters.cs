using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Microsoft.VisualStudio.Imaging;
using UltimateStartPage.Core.Models;

namespace UltimateStartPage.ToolWindows
{
    /// <summary>Picks the Visual Studio image-catalog icon for a link kind.</summary>
    public sealed class LinkKindToMonikerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value is LinkKind kind ? kind : LinkKind.File)
            {
                case LinkKind.Solution: return KnownMonikers.Solution;
                case LinkKind.Project: return KnownMonikers.Application;
                case LinkKind.Folder: return KnownMonikers.FolderClosed;
                case LinkKind.Url: return KnownMonikers.Link;
                default: return KnownMonikers.Document;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>true → Visible, false → Collapsed; pass "Invert" as the parameter to flip it.</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Visible when the bound string is empty (placeholder text); "Invert" gives visible-when-not-empty.</summary>
    public sealed class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var empty = string.IsNullOrEmpty(value as string);
            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            {
                empty = !empty;
            }

            return empty ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Visible when the bound count is zero — for "nothing here yet" hints.</summary>
    public sealed class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Visible when any of the bound booleans is true (for the left-hand column).</summary>
    public sealed class AnyTrueToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            foreach (var value in values)
            {
                if (value is bool b && b)
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
