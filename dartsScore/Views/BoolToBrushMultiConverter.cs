using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace dartsScore.Views
{
    /// <summary>
    /// Мульти-конвертер, принимающий список значений (обычно два булевых):
    /// <list type="bullet">
    /// <item><description>values[0] = isCurrent — текущий столбец раундов</description></item>
    /// <item><description>values[1] = isActiveCell — ячейка принадлежит активному игроку</description></item>
    /// </list>
    /// Возвращает цвет:
    /// <list type="bullet">
    /// <item><description>LightGreen — активная ячейка текущего игрока</description></item>
    /// <item><description>LightGoldenrodYellow — текущий столбец</description></item>
    /// <item><description>Transparent — иначе</description></item>
    /// </list>
    /// </summary>
    public class BoolToBrushMultiConverter : IMultiValueConverter
    {
        /// <summary>
        /// Преобразует массив входных значений в кисть (цвет).
        /// </summary>
        // Метод Convert - вычисляет кисть (цвет) на основе массива значений из MultiBinding
        public object? Convert(System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            try
            {
                bool isCurrent = false;
                bool isActiveCell = false;
                if (values.Count > 0 && values[0] is bool b0) isCurrent = b0;
                if (values.Count > 1 && values[1] is bool b1) isActiveCell = b1;

                if (isActiveCell)
                    return Brushes.LightGreen;
                if (isCurrent)
                    return Brushes.LightGoldenrodYellow;
                return Brushes.Transparent;
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        public System.Collections.Generic.IList<object?>? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
