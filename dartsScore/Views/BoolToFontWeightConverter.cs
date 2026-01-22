using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace dartsScore.Views
{
    /// <summary>
    /// Конвертер булевого значения в <see cref="FontWeight"/> (жирность шрифта).
    /// Используется в XAML для выделения имени активного игрока в списке (жирный/обычный).
    /// </summary>
    public class BoolToFontWeightConverter : IValueConverter
    {
        /// <summary>
        /// Преобразует булево значение в <see cref="FontWeight"/>.
        /// Возвращает <see cref="FontWeight.Bold"/> для true и <see cref="FontWeight.Normal"/> для false.
        /// </summary>
        /// <param name="value">Входное значение, ожидается булево.</param>
        /// <param name="targetType">Тип целевого свойства (игнорируется).</param>
        /// <param name="parameter">Параметр конвертера (не используется).</param>
        /// <param name="culture">Культура (не используется).</param>
        /// <returns>Значение <see cref="FontWeight"/>.</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool b = false;
            if (value is bool vb) b = vb;
            return b ? FontWeight.Bold : FontWeight.Normal;
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
