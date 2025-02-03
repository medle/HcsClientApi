
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.RemoteCaller;

//using HouseManagement = Hcs.Service.Async.HouseManagement.v13_2_3_3;
// переключение на версию 14.1.0.0 (12.03.2024)
//using HouseManagement = Hcs.Service.Async.HouseManagement.v14_1_0_0;
// переключение на версию 14.5.0.1 (11.09.2024)
using HouseManagement = Hcs.Service.Async.HouseManagement.v14_5_0_1;

namespace Hcs.Service.Async.HouseManagement.v14_5_0_1
{
    public partial class AckRequestAck : IHcsAck { }
    public partial class getStateResult : IHcsGetStateResult { }
    public partial class Fault : IHcsFault { }
    public partial class HeaderType : IHcsHeaderType { }
}

namespace Hcs.ClientApi.HouseManagementApi
{
    public class HcsHouseManagementMethod : HcsRemoteCallMethod
    {
        public HcsEndPoints EndPoint => HcsEndPoints.HomeManagementAsync;

        public HouseManagement.RequestHeader CreateRequestHeader() =>
            HcsRequestHelper.CreateHeader<HouseManagement.RequestHeader>(ClientConfig);

        public HcsHouseManagementMethod(HcsClientConfig config) : base(config) {}

        public System.ServiceModel.EndpointAddress RemoteAddress
            => GetEndpointAddress(HcsConstants.EndPointLocator.GetPath(EndPoint));

        private HouseManagement.HouseManagementPortsTypeAsyncClient NewPortClient()
        {
            var client = new HouseManagement.HouseManagementPortsTypeAsyncClient(_binding, RemoteAddress);
            ConfigureEndpointCredentials(client.Endpoint, client.ClientCredentials);
            return client;
        }

        public async Task<IHcsGetStateResult> SendAndWaitResultAsync(
            object request,
            Func<HouseManagement.HouseManagementPortsTypeAsyncClient, Task<IHcsAck>> sender,
            CancellationToken token)
        {
            while (true) {
                token.ThrowIfCancellationRequested();

                try {
                    if (CanBeRestarted) { // на случаи потери запросов и серверных сбоев в ГИС повторяем если можно
                        return await RunRepeatableTaskInsistentlyAsync(
                            async () => await SendAndWaitResultAsyncImpl(request, sender, token), token);
                    }
                    else { // разрешено только однократное исполнение
                        return await SendAndWaitResultAsyncImpl(request, sender, token);
                    }
                }
                catch (HcsRestartTimeoutException e) {
                    if (!CanBeRestarted) throw new HcsException("Превышен лимит ожидания выполнения запроса", e);
                    Log($"Перезапускаем запрос типа {request.GetType().Name}...");
                }
            }
        }

        private async Task<IHcsGetStateResult> SendAndWaitResultAsyncImpl(
            object request,
            Func<HouseManagement.HouseManagementPortsTypeAsyncClient, Task<IHcsAck>> sender,
            CancellationToken token)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            string version = HcsRequestHelper.GetRequestVersionString(request);
            _config.Log($"Отправляем запрос: {RemoteAddress.Uri}/{request.GetType().Name} в версии {version}...");

            var stopWatch = System.Diagnostics.Stopwatch.StartNew();

            IHcsAck ack;
            using (var client = NewPortClient()) {
                ack = await sender(client);
            }

            stopWatch.Stop();
            _config.Log($"Запрос принят в обработку за {stopWatch.ElapsedMilliseconds}мс., подтверждение {ack.MessageGUID}");

            var stateResult = await WaitForResultAsync(ack, true, token);

            // обнаруживаем ошибки возвращенные с сервера
            stateResult.Items.OfType<HouseManagement.ErrorMessageType>().ToList().ForEach(x => {
                throw HcsRemoteException.CreateNew(x.ErrorCode, x.Description);
            });

            return stateResult;
        }

        /// <summary>
        /// Выполняет однократную проверку наличия результата. 
        /// Возвращает null если результата еще нет.
        /// </summary>
        protected override async Task<IHcsGetStateResult> TryGetResultAsync(IHcsAck sourceAck, CancellationToken token)
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

        /// <summary>
        /// Разбирает стандартный ответ HCS на операцию импорта с приемом ошибок.
        /// </summary>
        protected HouseManagement.getStateResultImportResultCommonResult ParseSingleImportResult(IHcsGetStateResult stateResult)
        {
            return ParseImportResults(stateResult, 1, true).First();
        }

        /// <summary>
        /// Разбирает стандартный ответ HCS на операцию импорта с приемом ошибок.
        /// </summary>
        protected HouseManagement.getStateResultImportResultCommonResult[] ParseImportResults(
            IHcsGetStateResult stateResult, int commonResultRequiredCount, bool checkItemErrors)
        {
            var importResult = RequireSingleItem<HouseManagement.getStateResultImportResult>(stateResult.Items);
            if (IsArrayEmpty(importResult.Items)) throw new HcsException("Пустой ImportResult.Items");
            importResult.Items.OfType<HouseManagement.ErrorMessageType>().ToList()
                .ForEach(error => { throw HcsRemoteException.CreateNew(error.ErrorCode, error.Description); });

            var commonResults = importResult.Items.OfType<HouseManagement.getStateResultImportResultCommonResult>();

            // сначала проверяем ошибки, и только потом количество ответов,
            // ошибок может быть несколько для одного ожидаемого верного ответа
            foreach (var commonResult in commonResults) {
                if (IsArrayEmpty(commonResult.Items)) throw new HcsException("Пустой CommonResult.Items");
                if (checkItemErrors) CheckCommonResultErrors(commonResult);
            }

            if (commonResults.Count() != commonResultRequiredCount) {
                throw new HcsException(
                    $"Число результатов {commonResults.Count()} типа CommonResult не равно {commonResultRequiredCount}");
            }

            return commonResults.ToArray();
        }

        protected void CheckCommonResultErrors(HouseManagement.getStateResultImportResultCommonResult commonResult)
        {
            commonResult.Items.OfType<HouseManagement.CommonResultTypeError>().ToList()
                .ForEach(error => { throw HcsRemoteException.CreateNew(error.ErrorCode, error.Description); });
        }
    }
}
