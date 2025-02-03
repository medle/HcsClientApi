
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.DataTypes
{
    /// <summary>
    /// Сборный класс для получения сразу всех договоров, их лицевых счетов
    /// и приборов учета привязанных к лицевым счетам договоров.
    /// </summary>
    public class ГисДоговорыИПриборы
    {
        public DateTime ДатаНачалаСборки;
        public DateTime ДатаКонцаСборки;

        /// <summary>
        /// Договоры ресурсоснабжения.
        /// </summary>
        public List<ГисДоговор> ДоговорыРСО = new List<ГисДоговор>();

        public List<ГисАдресныйОбъект> АдресаОбъектов = new List<ГисАдресныйОбъект>();

        public List<ГисЗдание> Здания = new List<ГисЗдание>();

        public List<ГисЛицевойСчет> ЛицевыеСчета = new List<ГисЛицевойСчет>();

        public List<ГисПриборУчета> ПриборыУчета = new List<ГисПриборУчета>();

        public ГисДоговор НайтиДоговорПоНомеру(string номерДоговора) 
            => ДоговорыРСО.FirstOrDefault(x => x.НомерДоговора == номерДоговора);

        public bool ЭтотЛицевойСчетСвязанСДоговорами(ГисЛицевойСчет лс)
        {
            return ДоговорыРСО.Any(договор => лс.СвязанСДоговором(договор));
        }

        public ГисЗдание НайтиЗданиеПомещения(Guid гуидПомещения)
        {
            foreach (var здание in Здания) {
                foreach (var помещение in здание.Помещения) {
                    if (помещение.ГуидПомещения == гуидПомещения) return здание;
                }
            }

            return null;
        }

        public ГисЗдание НайтиЗданиеЛицевогоСчета(ГисЛицевойСчет лс)
        {
            if (лс.Размещения == null) return null;

            foreach (var размещение in лс.Размещения) {
                if (размещение.ГуидПомещения == null) continue;
                var здание = НайтиЗданиеПомещения((Guid)размещение.ГуидПомещения);
                if (здание != null) return здание;
            }

            return null;
        }

        public void УдалитьЛицевыеСчетаЗдания(Guid гуидЗданияФиас)
        {
            var здание = Здания.FirstOrDefault(x => x.ГуидЗданияФиас == гуидЗданияФиас);
            if (здание == null || здание.Помещения == null) return;

            var лсДляУдаления = new List<ГисЛицевойСчет>();
            var гуидыПомещенийЗдания = new HashSet<Guid>(здание.Помещения.Select(x => x.ГуидПомещения));

            foreach (var лс in ЛицевыеСчета) {
                if (лс.Размещения == null) continue;
                foreach (var размещениеЛС in лс.Размещения) {
                    if (размещениеЛС.ГуидПомещения == null) continue;
                    if (гуидыПомещенийЗдания.Contains((Guid)размещениеЛС.ГуидПомещения)) {
                        лсДляУдаления.Add(лс);
                        break;
                    }
                }
            }

            foreach (var лсУдалить in лсДляУдаления)
                ЛицевыеСчета.Remove(лсУдалить);
        }

        public int ЗаменитьЛицевыеСчетаЗданияВЛокальномСнимке(
            Guid гуидЗданияФиас, IEnumerable<ГисЛицевойСчет> лицевые)
        {
            УдалитьЛицевыеСчетаЗдания(гуидЗданияФиас);

            var живые = лицевые.Where(лс => лс.ДействуетСейчас && ЭтотЛицевойСчетСвязанСДоговорами(лс));
            ЛицевыеСчета.AddRange(живые);
            return живые.Count();
        }

        public bool ЭтотПриборСвязанСЛицевымиСчетами(ГисПриборУчета прибор)
        {
            return ЛицевыеСчета.Any(лс => прибор.СвязанСЛицевымСчетом(лс));
        }

        public IEnumerable<ГисАдресныйОбъект> ДатьАдресаОбъектовДоговора(ГисДоговор договор)
        {
            return АдресаОбъектов.Where(x => x.СвязанСДоговором(договор));
        }

        public IEnumerable<ГисПриборУчета> ДатьПриборыУчетаДоговора(ГисДоговор договор)
        {
            var адресаДоговора = ДатьАдресаОбъектовДоговора(договор).ToArray();
            var лицевыеДоговора = ЛицевыеСчета.Where(x => x.СвязанСДоговором(договор)).ToArray();

            var приборы = new List<ГисПриборУчета>();
            foreach (var прибор in ПриборыУчета) {
                if (прибор == null) continue;

                if (договор.ЭтоДоговорИКУ) {
                    // foreach на массиве быстрее чем linq во много раз
                    foreach (var адрес in адресаДоговора) {
                        if (прибор.СвязанСАдреснымОбъектом(адрес)) приборы.Add(прибор);
                    }
                }

                if (договор.ЭтоДоговорНежилогоПомещения) {
                    // foreach на массиве быстрее чем linq во много раз
                    foreach (var лицевой in лицевыеДоговора) {
                        if (прибор.СвязанСЛицевымСчетом(лицевой)) приборы.Add(прибор);
                    }
                }
            }

            return приборы;
        }

        public IEnumerable<ГисПомещение> ДатьПомещенияАдресногоОбъекта(ГисАдресныйОбъект адрес)
        { 
            var здание = Здания.FirstOrDefault(x => x.ГуидЗданияФиас == адрес.ГуидЗданияФиас);
            if (здание == null) return new List<ГисПомещение>();
            return здание.Помещения;
        }

        public IEnumerable<ГисПриборУчета> ДатьПриборыУчетаЛицевогоСчета(ГисЛицевойСчет лс)
        {
            return ПриборыУчета.Where(x => x.СвязанСЛицевымСчетом(лс));
        }

        public ГисЗданиеПомещение НайтиПомещениеЛицевогоСчета(ГисЛицевойСчет лс)
        {
            foreach (var размещение in лс.Размещения) {
                foreach (var здание in Здания) {
                    if (здание.Помещения == null) continue;
                    foreach (var помещение in здание.Помещения) {
                        if (помещение.ГуидПомещения == размещение.ГуидПомещения) {
                            return new ГисЗданиеПомещение(здание, помещение);
                        }
                    }
                }
            }
            return new ГисЗданиеПомещение(null, null);
        }

        public static ГисДоговорыИПриборы ПрочитатьФайлJson(string jsonFileName)
        {
            using (StreamReader file = File.OpenText(jsonFileName)) {
                JsonSerializer serializer = new JsonSerializer();
                return (ГисДоговорыИПриборы)serializer.Deserialize(file, typeof(ГисДоговорыИПриборы));
            }
        }

        public void ЗаписатьФайлJson(string jsonFileName)
        {
            using (StreamWriter file = File.CreateText(jsonFileName)) {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, this);
            }
        }

        public override string ToString()
        {
            return $"ДоговорыРСО={ДоговорыРСО.Count} Адреса={АдресаОбъектов.Count}" +
                   $" ЛС={ЛицевыеСчета.Count} ПУ={ПриборыУчета.Count}";
        }
    }
}
