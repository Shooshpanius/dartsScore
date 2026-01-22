using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace dartsScore.Views
{
    /// <summary>
    /// Конвертер булевого значения в <see cref="IBrush"/> (цвет заливки).
    /// Применяется для подсветки элементов интерфейса в зависимости от логических флагов.
    /// Значения кистей настраиваются через свойства <see cref="TrueBrush"/> и <see cref="FalseBrush"/>.
    /// </summary>
    /// <example>
    /// Пример использования в XAML: <code>&lt;Binding Path="IsActive" Converter="{StaticResource BoolToBrush}" /&gt;</code>
    /// </example>
    public class BoolToBrushConverter : IValueConverter
    {
        public IBrush TrueBrush { get; set; } = Brushes.LightGoldenrodYellow;
        public IBrush FalseBrush { get; set; } = Brushes.Transparent;
        /// <summary>
        /// Преобразует булево значение в кисть. Если входное значение true — возвращает <see cref="TrueBrush"/>, иначе — <see cref="FalseBrush"/>.
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool b = false;
            if (value is bool vb) b = vb;
            return b ? TrueBrush : FalseBrush;
        }

        /// <summary>
        /// Обратное преобразование не поддерживается.
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
