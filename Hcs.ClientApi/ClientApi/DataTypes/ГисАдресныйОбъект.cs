    
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.DataTypes
{
    /// <summary>
    /// Информация о зданиях и помещениях в зданиях (объектах жилого фонда),
    /// которые связаны с договором (в договоре имеются Лицевые Счета в
    /// указанных объектах). 
    /// </summary>
    public class ГисАдресныйОбъект
    {
        public Guid ГуидДоговора;
        public Guid ГуидВерсииДоговора;
        public Guid ГуидЗданияФиас;
        public Guid ГуидАдресногоОбъекта;

        public class ИзвестныеТипыЗдания { 
            public const string MKD = "MKD";
            public const string ZHD = "ZHD";
            public const string ZHDBlockZastroyki = "ZHDBlockZastroyki";
        }

        public string ТипЗдания;
        public string НомерПомещения;
        public string НомерКомнаты;

        public bool СвязанСДоговором(ГисДоговор договор) => договор != null && договор.ГуидДоговора == ГуидДоговора;

        public override string ToString()
        {
            return $"Тип=[{ТипЗдания}] ЗданиеФиас=[{ГуидЗданияФиас}] Объект[{ГуидАдресногоОбъекта}] Помещ[{НомерПомещения}] Комн[{НомерКомнаты}]";
        }
    }
}
