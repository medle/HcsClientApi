
using System;
using System.Linq;

using Hcs.ClientApi;

namespace Hcs.ClientDemo
{
    public class Program 
    {
        /// <summary>
        /// Демонстрационная программа вызова функций ГИС ЖКХ.
        /// </summary>
        public static void Main(string[] args)
        {
            // чтобы сообщения об ошибках показывались на английском языке
            System.Threading.Thread.CurrentThread.CurrentUICulture =
               new System.Globalization.CultureInfo("en-US");

            var client = new HcsClient();
            client.Logger = new HcsConsoleLogger();

            // чтобы создавались файлы сообщений и ответов системы
            //client.MessageCapture = new HcsFileWriterMessageCapture(null, client.Logger);

            // выбираем сертификат подписи сообщений, сертификат должен быть прикреплен
            // к в ЛК ГИСЖКХ в разделе  Инофрмационные системы/Сведения о системе
            //var cert = client.FindCertificate(x => x.Subject.Contains("Фамилия"));
            // ФВС
            //var cert = client.FindCertificate(x => x.SerialNumber == "011AE0730074AF38864558F638F51338A4");
            // ТАП
            var cert = client.FindCertificate(x => x.SerialNumber == "02DD0FE0006DB0C5B24666AB8F30C74780");
            if (cert == null) return;
            Console.WriteLine("Certificate: " + cert.Subject);
            client.SetSigningCertificate(cert);

            // промышленный или тестовый стенд 
            client.IsPPAK = true;
            if (client.IsPPAK) {
                // GUID поставщика информации ЭКК ППАК (20.05.2022)
                client.OrgPPAGUID = "488d95f6-4f6a-4e4e-b78a-ea259ef0ded2";
                // исполнитель/cотрудник ГИСЖКХ: ЛСА
                client.ExecutorGUID = "e0cba564-b675-4077-b7da-356b18301bc2";
                // исполнитель/cотрудник ГИСЖКХ: КЛА
                //client.ExecutorGUID = "c58c4cab-1849-48d2-976b-9cb93be4fc38";
            }
            else { // тестовый стенд
                // GUID поставщика информации ЭКК СИТ01 (21.04.2022)
                client.OrgPPAGUID = "3a16bc99-3016-42cd-b088-52106be6fa99";
                client.ExecutorGUID = "d1abb66b-2675-420b-857b-dd8ff3169ab5";

                // GUID поставщика информации ЭКК СИТ02 (18.01.2024)
                //client.OrgPPAGUID = "ee6b2615-c488-420c-a553-0ef31d65b77e";
                // сотрудник тестового стенда СИТ02
                //client.ExecutorGUID = "d284368e-849c-4002-a815-c8b199d35b05";
            }

#pragma warning disable CS0162 // disable warning "Unreachable code detected"
            try {
                if (false) DebtRequestsDemo.DemoExportOneDebtRequest(client);
                if (false) DebtRequestsDemo.DemoExportManySubrequests(client);
                if (false) DebtRequestsDemo.DemoImportOneDebtResponse(client);

                if (true) HouseManagementDemo.DemoExportOneHouse(client);
                if (false) HouseManagementDemo.DemoExportSupplyResourceContracts(client);
                if (false) HouseManagementDemo.DemoExportAccounts(client);
                if (false) HouseManagementDemo.DemoExportContractAddressObjects(client);
                if (false) HouseManagementDemo.DemoExportMeteringDevices(client);
                if (false) HouseManagementDemo.DemoExportOneContract(client);
                if (false) HouseManagementDemo.DemoExportContractTrees(client);
                if (false) HouseManagementDemo.DemoImportNewContract(client);
                if (false) HouseManagementDemo.DemoExportOrgRegistry(client);
                if (false) FileStoreDemo.DemoDownloadFile(client);
                if (false) FileStoreDemo.DemoGostHash(client);
                if (false) FileStoreDemo.DemoUploadFile(client);
                if (false) FileStoreDemo.DemoGetFileLength(client);
                if (false) FileStoreDemo.DemoGostHash(client);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.ReadKey();
            }
#pragma warning restore CS0162
        }
    }
}
