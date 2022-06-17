
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hcs.ClientApi.Config;
using Hcs.ClientApi.Providers;
using HouseManagement = Hcs.Service.Async.HouseManagement.v13_1_10_1;

namespace Hcs.ClientApi
{
    public class HcsHouseExporter : HcsWorkerBase
    {
        public HcsHouseExporter(HcsClientConfig config) : base(config)
        {
        }

        public async Task<string> ExportHouseByFiasGuid(Guid fiasHouseGuid)
        {
            var requestHeader = HcsRequestHelper.CreateHeader<HouseManagement.RequestHeader>(ClientConfig);
            var requestBody = new HouseManagement.exportHouseRequest {
                Id = HcsConstants.SignElementId,
                FIASHouseGuid = "60d080fc-f711-470f-bd21-eab217de2230", // Петрозаводск, Андропова, 10
                version = "12.2.0.1" // это значение из сообщения об ошибке если указать "13.1.10.1"
            };

            var request = new HouseManagement.exportHouseDataRequest {
                RequestHeader = requestHeader,
                exportHouseRequest = requestBody
            };

            var provider = new HouseManagmentProvider(ClientConfig);
            var ack = await provider.SendAsync(request);
            var result = await provider.WaitForResultAsync(ack, true);

            result.Items.OfType<HouseManagement.ErrorMessageType>().ToList().ForEach(x => {
                throw new HcsRemoteException(x.ErrorCode, x.Description);
            });

            string houseNumber = null;
            result.Items.OfType<HouseManagement.exportHouseResultType>().ToList().ForEach(x => {
                houseNumber = x.HouseUniqueNumber;
            });

            return houseNumber;
        }
    }
}
