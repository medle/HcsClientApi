using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.DataTypes
{
    /// <summary>
    /// Жилое или нежилое момещение в доме ГИС ЖКХ.
    /// </summary>
    public class ГисПомещение
    {
        public Guid ГуидПомещения;
        public bool ЭтоЖилоеПомещение;
        public string НомерПомещения;
        public DateTime? ДатаПрекращения;
        public string Аннулирование;

        public override string ToString()
        {
            return $"ГисПомещение={НомерПомещения} Жилое={ЭтоЖилоеПомещение} Guid={ГуидПомещения} Прекращено={ДатаПрекращения} Аннул={Аннулирование}";
        }
    }
}
