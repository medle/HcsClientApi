using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.DataTypes
{
    /// <summary>
    /// Дом ГИС ЖКХ.
    /// </summary>
    public class ГисЗдание
    {
        public ГисТипДома ТипДома;
        public Guid ГуидЗданияФиас;
        public string НомерДомаГис;

        public ГисПомещение[] Помещения;

        public override string ToString()
        {
            return $"{ТипДома} дом №ГИС={НомерДомаГис} Помещения={Помещения.Count()}";
        }
    }

    public enum ГисТипДома { Многоквартирный, Жилой };
}
