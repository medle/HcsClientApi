using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.DataTypes
{
    /// <summary>
    /// Лицевой счет из ГИС ЖКХ имеет номер единого лицевого счета (ЕЛС)
    /// и выдается на каждую точку поставки в жилом доме для договора
    /// по нежилым помещениям.
    /// </summary>
    public class ГисЛицевойСчет
    {
        public Guid ГуидЛицевогоСчета;
        public string НомерЛицевогоСчета;
        public string НомерЕЛС;

        public DateTime? ДатаСоздания;
        public DateTime? ДатаЗакрытия;
        public string КодНсиПричиныЗакрытия;
        public string ИмяПричиныЗакрытия;

        public decimal? ПолнаяПлощадь;
        public decimal? ЖилаяПлощадь;
        public string КодЖКУ;

        public ГисРазмещениеЛС[] Размещения;
        public ГисОснованиеЛС[] Основания;

        public bool СвязанСДоговором(ГисДоговор договор)
        {
            if (договор != null && Основания != null && Основания.Any(
                основание => основание.ГуидДоговора == договор.ГуидВерсииДоговора ||
                             основание.ГуидДоговора == договор.ГуидДоговора ||
                             string.Compare(основание.НомерДоговора, договор.НомерДоговора) == 0)) return true;
            return false;
        }

        [JsonIgnore]
        public bool ДействуетСейчас => (ДатаЗакрытия == null);

        [JsonIgnore]
        public string ОписаниеРазмещений
        {
            get {
                var accomod = new StringBuilder();
                foreach (var x in Размещения) accomod.Append($"[{x}]");
                return accomod.ToString();
            }
        }

        [JsonIgnore]
        public string ОписаниеОснований
        {
            get {
                if (Основания == null) return null;
                var reasons = new StringBuilder();
                foreach (var x in Основания) reasons.Append($"[{x}]");
                return reasons.ToString();
            }
        }

        public override string ToString()
        {
            return $"ЛС №{НомерЛицевогоСчета} ЕЛС={НомерЕЛС}" +
                   $" Создан={HcsUtil.FormatDate(ДатаСоздания)}" +
                   $" Закрыт={HcsUtil.FormatDate(ДатаЗакрытия)}" +
                   $" Размещения={ОписаниеРазмещений}" +
                   $" Основания={ОписаниеОснований}";
        }
    }

    /// <summary>
    /// Лицевой счет может быть привязан к нескольким размещениям.
    /// Каждое размещение может быть или в здании, или в жилой комнате или в помещении.
    /// </summary>
    public class ГисРазмещениеЛС
    {
        public Guid? ГуидЗдания; // неизвестно что он содержит
        public Guid? ГуидПомещения;
        public Guid? ГуидЖилойКомнаты;
        public decimal? ПроцентДоли;

        public override string ToString()
        {
            if (ГуидЗдания != null) return $"Здание={ГуидЗдания}";
            if (ГуидПомещения != null) return $"Помещение={ГуидПомещения}";
            if (ГуидЖилойКомнаты != null) return $"ЖилКомната={ГуидЖилойКомнаты}";
            return "";
        }
    }

    public enum ГисТипОснованияЛС { ДоговорРСО, Соцнайм, Договор }

    /// <summary>
    /// Основание создания лицевого счета (договор на основании которого открыт ЛС).
    /// </summary>
    public class ГисОснованиеЛС
    {
        public ГисТипОснованияЛС ТипОснованияЛС;
        public Guid ГуидДоговора;
        public string НомерДоговора;

        public override string ToString()
        {
            return $"{ТипОснованияЛС}={ГуидДоговора}";
        }
    }
}
