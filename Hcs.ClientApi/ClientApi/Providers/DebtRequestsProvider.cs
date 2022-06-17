
using System;
using System.Threading.Tasks;

using Hcs.ClientApi.Config;
using Hcs.ClientApi.Interfaces;
using DebtRequests = Hcs.Service.Async.DebtRequests.v13_1_10_1;

namespace Hcs.Service.Async.DebtRequests.v13_1_10_1
{
    public partial class AckRequestAck : IHcsAck { }
    public partial class getStateResult : IHcsGetStateResult { }
    public partial class Fault : IHcsFault { }
    public partial class HeaderType : IHcsHeaderType { }
}

namespace Hcs.ClientApi.Providers
{
    /// <summary>
    /// Служит для отправки запросов к сервису DebtRequestsAsync
    /// Описание: http://open-gkh.ru/DebtRequestsServiceAsync/
    /// </summary>
    public class DebtRequestsProvider : HcsServiceProviderBase, IHcsProvider
    {
        public HcsEndPoints EndPoint => HcsEndPoints.DebtRequestsAsync;

        public DebtRequestsProvider(HcsClientConfig config) : base(config)
        {
        }

        private DebtRequests.DebtRequestsAsyncPortClient NewPortClient()
        {
            var remoteAddress = GetEndpointAddress(HcsConstants.EndPointLocator.GetPath(EndPoint));
            var client = new DebtRequests.DebtRequestsAsyncPortClient(_binding, remoteAddress);
            ConfigureEndpointCredentials(client.Endpoint, client.ClientCredentials);
            return client;
        }

        /// <summary>
        /// Метод отправления запроса.
        /// </summary>
        public async Task<IHcsAck> SendAsync(object request)
        {
            if (request == null) throw new ArgumentNullException("Null request");
            _config.Log($"Отправляем запрос {request.GetType().Name}...");

            IHcsAck ack;
            using (var client = NewPortClient()) {
                switch (request) {

                    case DebtRequests.exportDebtSubrequestsRequest x: {
                            var response = await client.exportDebtSubrequestsAsync(x.RequestHeader, x.exportDSRsRequest);
                            ack = response.AckRequest.Ack;
                            break;
                        }

                    case DebtRequests.importResponsesRequest x: {
                            var response = await client.importResponsesAsync(x.RequestHeader, x.importDSRResponsesRequest);
                            ack = response.AckRequest.Ack;
                            break;
                        }

                    default:
                        throw new HcsException($"Неизвестный тип запроса: {request.GetType().Name}");
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
                var requestHeader = HcsRequestHelper.CreateHeader<DebtRequests.RequestHeader>(_config);
                var requestBody = new DebtRequests.getStateRequest { MessageGUID = sourceAck.MessageGUID };

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
