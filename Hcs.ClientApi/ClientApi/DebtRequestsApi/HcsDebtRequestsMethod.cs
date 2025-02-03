
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.RemoteCaller;

// переключение на версию 13.2.3.3 (13.12.2023)
//using DebtRequests = Hcs.Service.Async.DebtRequests.v13_2_3_3;
// переключение на версию 14.0.0.0 (23.01.2024)
//using DebtRequests = Hcs.Service.Async.DebtRequests.v14_0_0_0;
// переключение на версию 14.1.0.0 (12.03.2024)
//using DebtRequests = Hcs.Service.Async.DebtRequests.v14_1_0_0;
// переключение на версию 14.5.0.1 (11.09.2024)
using DebtRequests = Hcs.Service.Async.DebtRequests.v14_5_0_1;

namespace Hcs.Service.Async.DebtRequests.v14_5_0_1
{
    public partial class AckRequestAck : IHcsAck { }
    public partial class getStateResult : IHcsGetStateResult { }
    public partial class Fault : IHcsFault { }
    public partial class HeaderType : IHcsHeaderType { }
}

namespace Hcs.ClientApi.DebtRequestsApi
{
    /// Метод для отправки запросов к сервису запросов о наличии задолженности
    /// Описание: http://open-gkh.ru/DebtRequestsServiceAsync/
    public class HcsDebtRequestsMethod : HcsRemoteCallMethod
    {
        public HcsEndPoints EndPoint => HcsEndPoints.DebtRequestsAsync;

        public HcsDebtRequestsMethod(HcsClientConfig config) : base(config)
        { 
        }

        public DebtRequests.RequestHeader CreateRequestHeader() =>
            HcsRequestHelper.CreateHeader<DebtRequests.RequestHeader>(ClientConfig);

        public System.ServiceModel.EndpointAddress RemoteAddress
            => GetEndpointAddress(HcsConstants.EndPointLocator.GetPath(EndPoint));

        private DebtRequests.DebtRequestsAsyncPortClient NewPortClient()
        {
            var client = new DebtRequests.DebtRequestsAsyncPortClient(_binding, RemoteAddress);
            ConfigureEndpointCredentials(client.Endpoint, client.ClientCredentials);
            return client;
        }

        /// <summary>
        /// Метод отправления запроса.
        /// </summary>
        public async Task<IHcsAck> SendAsync(object request, CancellationToken token)
        {
            Func<Task<IHcsAck>> func = async () => await SendBareAsync(request);
            return await RunRepeatableTaskInsistentlyAsync(func, token);
        }

        private async Task<IHcsAck> SendBareAsync(object request)
        {
            if (request == null) throw new ArgumentNullException("Null request");
            string version = HcsRequestHelper.GetRequestVersionString(request);
            _config.Log($"Отправляю {RemoteAddress.Uri}/{request.GetType().Name}" + 
                        $" в версии {version} {ThreadIdText}...");

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
        protected override async Task<IHcsGetStateResult> TryGetResultAsync(
            IHcsAck sourceAck, CancellationToken token = default)
        {
            Func<Task<IHcsGetStateResult>> func = async () => await TryGetResultBareAsync(sourceAck);
            return await RunRepeatableTaskInsistentlyAsync(func, token);
        }

        private async Task<IHcsGetStateResult> TryGetResultBareAsync(IHcsAck sourceAck)
        {
            using (var client = NewPortClient()) {
                var requestHeader = HcsRequestHelper.CreateHeader<DebtRequests.RequestHeader>(_config);
                var requestBody = new DebtRequests.getStateRequest { MessageGUID = sourceAck.MessageGUID };

                var response = await client.getStateAsync(requestHeader, requestBody);
                var resultBody = response.getStateResult;

                if (resultBody.RequestState == HcsAsyncRequestStateTypes.Ready) {
                    CheckResultForErrors(resultBody);
                    return resultBody;
                }

                return null;
            }
        }

        private void CheckResultForErrors(IHcsGetStateResult result)
        {
            if (result == null) throw new HcsException("Пустой result");

            if (result.Items == null) throw new HcsException("Пустой result.Items");

            result.Items.OfType<DebtRequests.Fault>().ToList().ForEach(x => {
                throw HcsRemoteException.CreateNew(x.ErrorCode, x.ErrorMessage);
            });

            result.Items.OfType<DebtRequests.ErrorMessageType>().ToList().ForEach(x => {
                throw HcsRemoteException.CreateNew(x.ErrorCode, x.Description);
            });
        }
    }
}
