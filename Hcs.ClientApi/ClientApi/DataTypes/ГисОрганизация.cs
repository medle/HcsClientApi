
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Hcs.ClientApi.DataTypes
{
    /// <summary>
    /// Сведения из реестра оргнаизации ГИС ЖКХ.
    /// </summary>
    public class ГисОрганизация
    {
        public Guid ГуидОрганизации;

        public Guid ГуидВерсииОрганизации;

        public ГисТипОрганизации ТипОрганизации;

        public string КраткоеИмяОрганизации;

        public string ПолноеИмяОрганизации;

        public bool Действующая;

        public string ИНН;

        public string КПП;

        public string ОГРН;

        public string ОКОПФ;

        public string Фамилия;

        public string Имя;

        public string Отчество;

        public string ЮридическийАдрес;

        public DateTime? ДатаЛиквидации;

        [JsonIgnore]
        public const int ДлинаОГРН = 13;

        [JsonIgnore]
        public const int ДлинаОГРНИП = 15;

        public override string ToString()
        {
            string имя = ТипОрганизации == ГисТипОрганизации.ИП ?
                $"ИП {Фамилия} {Имя} {Отчество}" : КраткоеИмяОрганизации;
            return $"{ТипОрганизации}: [{имя}] ИНН={ИНН} КПП={КПП} Действующая={Действующая}" +
                   $" ГУИД={ГуидОрганизации} Версия={ГуидВерсииОрганизации}";
        }
    }

    public enum ГисТипОрганизации { НетУказано, ЮЛ, ИП, Филиал, Иностранный }
}
