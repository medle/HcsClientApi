
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Hcs.ClientApi.DataTypes
{
    /// <summary>
    /// Сведения о контрагенте договора РСО.
    /// </summary>
    public class ГисКонтрагент
    {
        public ГисТипКонтрагента ТипКонтрагента;

        /// <summary>
        /// ГУИД из реестра организаций ГИС ЖКХ.
        /// </summary>
        public Guid? ГуидОрганизации;

        /// <summary>
        /// ГУИД версии организации из реестра организаций ГИС ЖКХ необходим
        /// для размещения Лицевого счета.
        /// </summary>
        public Guid? ГуидВерсииОрганизации;

        /// <summary>
        /// Сведения об индивидуальном физическом лице.
        /// </summary>
        public ГисИндивид Индивид;
    }

    public enum ГисТипКонтрагента
    { 
        НеУказано,
        ВладелецПомещения,
        УправляющаяКомпания
    }

    public class ГисИндивид
    {
        public string Фамилия;
        public string Имя;
        public string Отчество;
        public string СНИЛС;
        public string НомерДокумента;
        public string СерияДокумента;
        public DateTime? ДатаДокумента;

        [JsonIgnore]
        public bool СНИЛСЗаполнен 
            => !string.IsNullOrEmpty(СНИЛС);

        [JsonIgnore]
        public string СНИЛСТолькоЦифры 
            => СНИЛСЗаполнен ? string.Concat(СНИЛС.Where(char.IsDigit)) : null;

        [JsonIgnore]
        public bool СНИЛСЗаполненВернойДлины
            => (СНИЛСЗаполнен && СНИЛСТолькоЦифры.Length == 11);

        public void ПроверитьЗаполнениеСНИЛС()
        {
            if (!СНИЛСЗаполненВернойДлины)
                throw new HcsException($"В СНИЛС контрагента ФЛ должно быть указано 11 цифр: {СНИЛС}");
        }

        public void ПроверитьЗаполнениеФИО()
        {
            if (string.IsNullOrEmpty(Фамилия)) throw new HcsException("Не заполнена Фамилия контрагента ФЛ");
            if (string.IsNullOrEmpty(Имя)) throw new HcsException("Не заполнено Имя контрагента ФЛ");
        }
    }
}
