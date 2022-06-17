
using System;
using System.Linq;

using Hcs.ClientApi;

namespace Hcs.ClientDemo
{
    public class Program 
    {
        /// <summary>
        /// Демонстрационная программа вызова функций ГИСЖКХ.
        /// </summary>
        public static void Main(string[] args)
        {
            var client = new HcsClient();
            client.Logger = new HcsConsoleLogger();

            // чтобы создавались файлы сообщений и ответов системы
            client.MessageCapture = new HcsFileWriterMessageCapture(null, client.Logger);

            // выбираем сертификат подписи сообщений
            var cert = client.FindCertificate(x => x.Subject.Contains("Иванов"));
            if (cert == null) return;
            client.SetSigningCertificate(cert);

            // промышленная система 
            client.IsPPAK = true;
            if (client.IsPPAK) {
                // GUID поставщика информации ППАК (20.05.2022)
                client.OrgPPAGUID = "488d95f6-4f6a-4e4e-b78a-ea259ef0ded2";
                // исполнитель/cотрудник ГИСЖКХ
                client.ExecutorGUID = "e0cba564-b675-4077-b7da-356b18301bc2"; 
            }
            else { // тестовый стенд
                // GUID поставщика информации СИТ01 (21.04.2022)
                client.OrgPPAGUID = "3a16bc99-3016-42cd-b088-52106be6fa99"; 
            }

            try {
                //TestExportOneDebtRequest(client);
                //TestImportOneDebtResponse(client);
                TestExportHouse(client);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void TestExportManySubrequests(HcsClient client)
        {
            Action<HcsDebtSubrequest> handler = delegate (HcsDebtSubrequest s) { };

            var date = new DateTime(2022, 5, 16);
            int n = client.ExportDSRsByPeriodOfSending(date, date, handler).Result;

            client.Log($"Получено запросов: {n}");
        }

        private static void TestExportOneDebtRequest(HcsClient client)
        {
            var s = client.ExportDSRByRequestNumber("0520228792809").Result;
            client.Log(
               $"Получен ответ #{s.RequestNumber}" +
               $" Address=[{s.Address}] Details=[{s.AddressDetails}]" +
               $" Sent={s.SentDate} ResponseStatus={s.ResponseStatus}"
               );
        }

        private static void TestImportOneDebtResponse(HcsClient client)
        {
            var subrequest = client.ExportDSRByRequestNumber("0520228792809").Result;
            if (subrequest == null) Console.WriteLine("Error: subrequest not found");

            var response = new HcsDebtResponse();
            response.TransportGuid = Guid.NewGuid();
            response.SubrequestGuid = subrequest.SubrequestGuid;
            response.HasDebt = false;

            var result = client.ImportDSRResponse(response).Result;
            if (result.HasError) Console.WriteLine("Error occured during response sending: " + result.Error);
            else Console.WriteLine("Success sending response: " + result.UpdateDate);
        }

        private static void TestExportHouse(HcsClient client)
        {
            var guid = Guid.Parse("60d080fc-f711-470f-bd21-eab217de2230"); // Петрозаводск, Андропова, 10
            var number = client.ExportHouseByFiasGuid(guid).Result;
            Console.WriteLine("house number=" + number);
        }
    }
}
