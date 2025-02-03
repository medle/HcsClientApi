
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

using Hcs.ClientApi;
using Hcs.ClientApi.DataTypes;
using System.Threading;

namespace Hcs.ClientDemo
{
    public class HouseManagementDemo
    {
        public static void DemoExportOrgRegistry(HcsClient client)
        {
            //string ogrn = "317100100012888"; // ОГРНИП
            string ogrn = "1061001043421";
            string kpp = "";
            var orgs = client.OrgRegistryCommon.GetOrgByOgrn(ogrn, kpp).Result;
            Console.WriteLine($"Организация с ОГРН={ogrn} имеет {orgs.Count()} orgs:");
            foreach (var org in orgs) 
                Console.WriteLine($"  {org}");
        }

        public static void DemoExportOneContract(HcsClient client)
        {
            //var guid = new Guid("18ce8fae-5747-4662-95b4-d5048e8d3e40"); // ТСЖ
            //var guid = new Guid("1e639ca1-ce77-4dc9-a767-fa70339c2486"); // АРЕЯ 7029.0
            //var guid = new Guid("b9100a42-f4e9-4802-a297-c339eb0c515f"); // ТСЖ Центральное
            //var guid = new Guid("acdadadf-28b6-4250-952c-b546850b4229"); // ТСН Сулажгорская 29А (СОИ)
            //var guid = new Guid("e02deaa4-e1a6-4255-844a-d848da99c89a"); // ТСЖ Онежский берег
            //var guid = new Guid("e73006b2-2fd9-4201-bb84-5cb68b5482ed"); // АВЕРС
            //var guid = new Guid("c374bf70-3108-421d-bb96-3d866a135866"); // теплоавтоматика агент
            var guid = new Guid("2d393e41-b7e2-4125-9593-c4127617e3f8"); // тестовый

            var договор = client.HouseManagement.ПолучитьДоговорРСО(guid).Result;
            Console.WriteLine($"Получен договор №{договор.НомерДоговора} Статус={договор.СтатусВерсииДоговора}");

            if (договор.ПриложенияДоговора != null && договор.ПриложенияДоговора.Length > 0) {
                var приложение = договор.ПриложенияДоговора[0];
                Console.WriteLine($"Приложение: {приложение.ИмяПриложения} HASH={приложение.ХэшПриложения}");
                FileStoreDemo.PrintFileHash(client, $"d:\\\\temp\\{приложение.ИмяПриложения}");
            }
        }

        public static void DemoTerminateOneContract(HcsClient client)
        {
            var guid = new Guid("c7418f95-8ec5-40a3-9474-c4924e17409e");
            var договор = client.HouseManagement.ПолучитьДоговорРСО(guid).Result;
            Console.WriteLine($"Получен договор №{договор.НомерДоговора} Статус={договор.СтатусВерсииДоговора}");

            var d = client.HouseManagement.РасторгнутьДоговор(договор, new DateTime(2019, 4, 1)).Result;
            Console.WriteLine($"Дата внесения расторжения договора: {d}");
        }

        public static void DemoImportNewContract(HcsClient client)
        {
            // Договор №30 (Химчистка Радуга)
            var договор = new ГисДоговор();
            договор.ТипДоговораРСО = ГисТипДоговораРСО.ПубличныйИлиНежилые;
            договор.НомерДоговора = "100-1-41-21900-01";
            договор.ДатаЗаключения = new DateTime(2007, 7, 1);

            договор.Контрагент = new ГисКонтрагент();
            // TODO: заполнить контрагента получив его GUID через OrgRegistryService по ОГРН

            var адреса = new List<ГисАдресныйОбъект>();
            // TODO: заполнить хотя-бы один адрес

            var d = client.HouseManagement.РазместитьДоговор(договор, адреса).Result;
            Console.WriteLine($"Дата внесения нового договора: {d}");
        }

        public static void DemoExportContractTrees(HcsClient client)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Func<ГисДоговор, bool> contractFilter = (договор) => {
                if (договор.СтатусВерсииДоговора == ГисСтатусВерсииДоговора.Аннулирован) return false;
                if (договор.СтатусВерсииДоговора == ГисСтатусВерсииДоговора.Расторгнут) return false;
                // ГИС возвращает проекты но не может их найти по коду
                if (договор.СтатусВерсииДоговора == ГисСтатусВерсииДоговора.Проект) return false;
                if ("ЭККУК" == договор.НомерДоговора) return false;
                if ("ЭККЧС" == договор.НомерДоговора) return false;
                if ("ЭККНСУ" == договор.НомерДоговора) return false;
                return true;
            };

            var все = client.HouseManagement.ПолучитьВсеДоговорыИПриборы(contractFilter).Result;

            using (StreamWriter file = File.CreateText(@"all.json")) {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, все);
            }

            stopwatch.Stop();
            Console.WriteLine($"{все}: {stopwatch.Elapsed}");
        }

        public static void DemoExportMeteringDevices(HcsClient client)
        {
            Action<ГисПриборУчета> resultHandler = (прибор) => {
                Console.WriteLine("" + прибор);
            };

            // ул. Мурманская, 1А
            var houseGuid = new Guid("0c94cace-2030-4b0a-97c4-de85eef83282");
            var n = client.HouseManagement.ПолучитьПриборыУчетаПоЗданию(houseGuid, resultHandler).Result;
            Console.WriteLine("n = " + n);
        }

        public static void DemoExportContractAddressObjects(HcsClient client)
        {
            Action<ГисАдресныйОбъект> resultHandler = (адрес) => {
                Console.WriteLine("" + адрес);
            };

            //var гуидДоговора = new Guid("401a609e-5150-498b-a5cf-b2a036eb898b"); 
            var гуидДоговора = new Guid("4f8b6688-ef14-43e6-99a9-846e59cd82e8"); // НСУ
            //var гуидДоговора = new Guid("a022affc-6269-4158-bb0d-e21faf000da7"); // ЭККУК

            var договор = new ГисДоговор() { ГуидДоговора = гуидДоговора };
            var n = client.HouseManagement.ПолучитьАдресаДоговораРСО(договор, resultHandler).Result;
            Console.WriteLine("n = " + n);
        }

        public static void DemoExportAccounts(HcsClient client)
        {
            Action<ГисЛицевойСчет> resultHandler = (лс) => {
                Console.WriteLine("" + лс);
            };

            // ул. Мурманская, 1А
            var houseGuid = new Guid("0c94cace-2030-4b0a-97c4-de85eef83282");
            var n = client.HouseManagement.ПолучитьЛицевыеСчетаПоЗданию(houseGuid, resultHandler).Result;
            Console.WriteLine("n = " + n);
        }

        public static void DemoExportSupplyResourceContracts(HcsClient client)
        {
            var договоры = new List<ГисДоговор>();
            Action<ГисДоговор> resultHandler = (договорРСО) => { договоры.Add(договорРСО); };
            var n = client.HouseManagement.ПолучитьДоговорыРСО(resultHandler).Result;

            договоры.Sort((x, y) => string.Compare(x.НомерДоговора, y.НомерДоговора));
            договоры.ForEach(x => Console.WriteLine(x.ToString()));
            Console.WriteLine("n = " + n);
        }

        public static void DemoExportOneHouse(HcsClient client)
        {
            //var guid = Guid.Parse("60d080fc-f711-470f-bd21-eab217de2230"); // Петрозаводск, Андропова, 10
            var guid = Guid.Parse("6596ad9d-fee2-4c5b-8249-dbf78b0281b9"); // Петрозаводск, Лисицыной, 19
            var здание = client.HouseManagement.ПолучитьЗданиеПоГуидФиас(guid).Result;
            Console.WriteLine("ГисЗдание=" + здание);
            foreach (var помещение in здание.Помещения) {
                Console.WriteLine(помещение.ToString());
            }
        }
    }
}
