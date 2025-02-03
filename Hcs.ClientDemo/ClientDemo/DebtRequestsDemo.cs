using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hcs.ClientApi;
using Hcs.ClientApi.DebtRequestsApi;

namespace Hcs.ClientDemo
{
    public class DebtRequestsDemo
    {
        public static void DemoExportManySubrequests(HcsClient client)
        {
            Action<HcsDebtSubrequest> handler = delegate (HcsDebtSubrequest s) {
                client.Log($"Получен: {s}");
            };

            var date = new DateTime(2024, 1, 22);
            int n = client.DebtRequests.ExportDSRsByPeriodOfSending(date, date, null, handler).Result;

            client.Log($"Получено запросов: {n}");
        }

        public static void DemoExportOneDebtRequest(HcsClient client)
        {
            HcsDebtSubrequest s;
            if(client.IsPPAK) s = client.DebtRequests.ExportDSRByRequestNumber("01202411454682").Result;
            else s = client.DebtRequests.ExportDSRByRequestNumber("0120241061").Result;
            client.Log($"Получен: {s}");
        }

        public static void DemoImportOneDebtResponse(HcsClient client)
        {
            HcsDebtSubrequest s;
            if (client.IsPPAK) s = client.DebtRequests.ExportDSRByRequestNumber("01202411454682").Result;
            else s = client.DebtRequests.ExportDSRByRequestNumber("0120241061").Result;
            if (s == null) Console.WriteLine("Error: subrequest not found");

            var response = new HcsDebtResponse();
            response.TransportGuid = Guid.NewGuid();
            response.SubrequestGuid = s.SubrequestGuid;

            // если указывается наличие долга обязательно указание ФИО должников
            response.HasDebt = false;
            //response.PersonalData = new HcsPersonalData[] { new HcsPersonalData() { 
            //    FirstName = "A", MiddleName = "B", LastName = "C"
            //}}; 

            var result = client.DebtRequests.ImportDSRResponse(response).Result;
            if (result.HasError) Console.WriteLine("Error occured during response sending: " + result.Error);
            else Console.WriteLine("Success sending response: " + result.UpdateDate);
        }
    }
}
