
using System;
using System.Threading.Tasks;

using Hcs.ClientApi.Config;
using Hcs.ClientApi.Interfaces;
using HouseManagement = Hcs.Service.Async.HouseManagement.v13_1_10_1;

namespace Hcs.Service.Async.HouseManagement.v13_1_10_1
{
    public partial class AckRequestAck : IHcsAck { }
    public partial class getStateResult : IHcsGetStateResult { }
    public partial class Fault : IHcsFault { }
    public partial class HeaderType : IHcsHeaderType { }
}

namespace Hcs.ClientApi.Providers
{
    /// <summary>
    /// Служит для отправки запросов к сервису HouseManagementAsync
    /// </summary>
    public class HouseManagmentProvider : HcsServiceProviderBase, IHcsProvider
    {
        public HcsEndPoints EndPoint => HcsEndPoints.HouseManagementAsync;

        public HouseManagmentProvider(HcsClientConfig config) : base(config)
        {
        }

        private HouseManagement.HouseManagementPortsTypeAsyncClient NewPortClient()
        {
            var remoteAddress = GetEndpointAddress(HcsConstants.EndPointLocator.GetPath(EndPoint));
            var client = new HouseManagement.HouseManagementPortsTypeAsyncClient(_binding, remoteAddress);
            ConfigureEndpointCredentials(client.Endpoint, client.ClientCredentials);
            return client;
        }

        /// <summary>
        /// Метод отравления запроса
        /// </summary>
        public async Task<IHcsAck> SendAsync(object request)
        {
            if (request == null) throw new ArgumentNullException("Null request");
            _config.Log($"Отправляем запрос {request.GetType().Name}...");

            IHcsAck ack;
            using (var client = NewPortClient()) {
                switch (request) {

                    case HouseManagement.exportHouseDataRequest x:
                        var response = await client.exportHouseDataAsync(x.RequestHeader, x.exportHouseRequest);
                        ack = response.AckRequest.Ack;
                        break;

                    default:
                        throw new ArgumentException($"{request.GetType().Name}: неизвестный тип запроса данных");
                }
            }

            _config.Log($"Запрос принят в обработку, подтверждение {ack.MessageGUID}");
            return ack;
        }

        /// <summary>
        /// Выполняет однократную проверку наличия результата. 
        /// Возвращает null если результата еще нет.
        /// </summary>
        public override async Task<IHcsGetStateResult> TryGetResultAsync(IHcsAck sourceAck)
        {
            using (var client = NewPortClient()) {
                var requestHeader = HcsRequestHelper.CreateHeader<HouseManagement.RequestHeader>(_config);
                var requestBody = new HouseManagement.getStateRequest { MessageGUID = sourceAck.MessageGUID };

                var response = await client.getStateAsync(requestHeader, requestBody);
                var resultBody = response.getStateResult;

                if (resultBody.RequestState == HcsAsyncRequestStateTypes.Ready) {
                    return resultBody;
                }

                return null;
            }
        }
    }
}
