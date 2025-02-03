
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.RemoteCaller;

using OrgRegistryCommon = Hcs.Service.Async.OrgRegistryCommon.v14_5_0_1;

namespace Hcs.Service.Async.OrgRegistryCommon.v14_5_0_1
{
    public partial class AckRequestAck : IHcsAck { }
    public partial class getStateResult : IHcsGetStateResult { }
    public partial class Fault : IHcsFault { }
    public partial class HeaderType : IHcsHeaderType { }
}

namespace Hcs.ClientApi.OrgRegistryCommonApi
{
    public class HcsOrgRegistryCommonMethod : HcsRemoteCallMethod
    {
        public HcsEndPoints EndPoint => HcsEndPoints.OrgRegistryCommonAsync;

        public OrgRegistryCommon.ISRequestHeader CreateRequestHeader() =>
            HcsRequestHelper.CreateHeader<OrgRegistryCommon.ISRequestHeader>(ClientConfig);

        public HcsOrgRegistryCommonMethod(HcsClientConfig config) : base(config) { }

        public System.ServiceModel.EndpointAddress RemoteAddress
            => GetEndpointAddress(HcsConstants.EndPointLocator.GetPath(EndPoint));

        private OrgRegistryCommon.RegOrgPortsTypeAsyncClient NewPortClient()
        {
            var client = new OrgRegistryCommon.RegOrgPortsTypeAsyncClient(_binding, RemoteAddress);
            ConfigureEndpointCredentials(client.Endpoint, client.ClientCredentials);
            return client;
        }

        public async Task<IHcsGetStateResult> SendAndWaitResultAsync(
            object request,
            Func<OrgRegistryCommon.RegOrgPortsTypeAsyncClient, Task<IHcsAck>> sender,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            while (true) {
                try {
                    return await SendAndWaitResultAsyncImpl(request, sender, token);
                }
                catch (HcsRestartTimeoutException e) {
                    if (!CanBeRestarted) throw new HcsException("Превышен лимит ожидания выполнения запроса", e);
                    Log($"Перезапускаем запрос типа {request.GetType().Name}...");
                }
            }
        }

        private async Task<IHcsGetStateResult> SendAndWaitResultAsyncImpl(
            object request,
            Func<OrgRegistryCommon.RegOrgPortsTypeAsyncClient, Task<IHcsAck>> sender,
            CancellationToken token)
        {
            if (request == null) throw new ArgumentNullException("Null request");
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
            stateResult.Items.OfType<OrgRegistryCommon.ErrorMessageType>().ToList().ForEach(x => {
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
                var requestHeader = HcsRequestHelper.CreateHeader<OrgRegistryCommon.ISRequestHeader>(_config);
                var requestBody = new OrgRegistryCommon.getStateRequest { MessageGUID = sourceAck.MessageGUID };

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
