namespace Configurator.Desktop.Resources;

using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Configurator.Presentation.Store;

/// <summary>Converts strictly validated square PNG bytes from a curated Store project card into an Avalonia <see cref="Bitmap"/>.</summary>
public sealed class StoreIconBitmapConverter : IValueConverter
{
    public static StoreIconBitmapConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ReadOnlyMemory<byte> bytes ||
            bytes.IsEmpty ||
            bytes.Length > StoreIconValidator.MaximumDecodedBytes ||
            !StoreIconValidator.IsValidSquarePngHeader(bytes.Span))
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes.ToArray(), writable: false);
            return new Bitmap(stream);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
