
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.Config;
using Hcs.ClientApi.Interfaces;

namespace Hcs.ClientApi.Providers
{
    public abstract class HcsServiceProviderBase
    {
        public HcsClientConfig _config;
        protected CustomBinding _binding;

        public HcsServiceProviderBase(HcsClientConfig config)
        {
            _config = config;

            _binding = new CustomBinding();

            _binding.Elements.Add(new TextMessageEncodingBindingElement {
                MessageVersion = MessageVersion.Soap11,
                WriteEncoding = Encoding.UTF8
            });

            if (config.UseTunnel)
            {
                if (Process.GetProcessesByName("stunnel").Any() ? false : true)
                {
                    throw new Exception("stunnel не запущен");
                }

                _binding.Elements.Add(new HttpTransportBindingElement {
                    AuthenticationScheme = (config.IsPPAK ? System.Net.AuthenticationSchemes.Digest : System.Net.AuthenticationSchemes.Basic),
                    MaxReceivedMessageSize = int.MaxValue,
                    UseDefaultWebProxy = false
                });
            }
            else
            {
                _binding.Elements.Add(new HttpsTransportBindingElement {
                    AuthenticationScheme = (config.IsPPAK ? System.Net.AuthenticationSchemes.Digest : System.Net.AuthenticationSchemes.Basic),
                    MaxReceivedMessageSize = int.MaxValue,
                    UseDefaultWebProxy = false,
                    RequireClientCertificate = true
                });
            }
        }

        public EndpointAddress GetEndpointAddress(string endpointName)
        {
            if (_config.UseTunnel)
                return new EndpointAddress($"http://{HcsConstants.Address.UriTunnel}/{endpointName}");

            return (_config.IsPPAK ? new EndpointAddress($"https://{HcsConstants.Address.UriPPAK}/{endpointName}")
                    : new EndpointAddress($"https://{HcsConstants.Address.UriSIT}/{endpointName}"));
        }

        public void ConfigureEndpointCredentials(
            ServiceEndpoint serviceEndpoint, ClientCredentials clientCredentials)
        {
            serviceEndpoint.EndpointBehaviors.Add(new GostSigningEndpointBehavior(_config));

            if (!_config.IsPPAK)
            {
                clientCredentials.UserName.UserName = HcsConstants.UserAuth.Name;
                clientCredentials.UserName.Password = HcsConstants.UserAuth.Passwd;
            }

            if (!_config.UseTunnel)
            {
                clientCredentials.ClientCertificate.SetCertificate(
                 StoreLocation.CurrentUser,
                 StoreName.My,
                 X509FindType.FindByThumbprint,
                 _config.CertificateThumbprint);
            }
        }

        public abstract Task<IHcsGetStateResult> TryGetResultAsync(IHcsAck sourceAck);

        /*
        Также рекомендуем придерживаться следующего алгоритма отправки запросов на получение статуса обработки пакета в случае использования асинхронных сервисов ГИС ЖКХ (в рамках одного MessageGUID):
        - первый запрос getState направлять не ранее чем через 10 секунд, после получения квитанции о приеме пакета с бизнес-данными от сервиса ГИС КЖХ;
        - в случае, если на первый запрос getSate получен результат с RequestState равным "1" или "2", то следующий запрос getState необходимо направлять не ранее чем через 60 секунд после отправки предыдущего запроса;
        - в случае, если на второй запрос getSate получен результат с RequestState равным "1" или "2", то следующий запрос getState необходимо направлять не ранее чем через 300 секунд после отправки предыдущего запроса;
        - в случае, если на третий запрос getSate получен результат с RequestState равным "1" или "2", то следующий запрос getState необходимо направлять не ранее чем через 900 секунд после отправки предыдущего запроса;
        - в случае, если на четвертый (и все последующие запросы) getSate получен результат с RequestState равным "1" или "2", то следующий запрос getState необходимо направлять не ранее чем через 1800 секунд после отправки предыдущего запроса.* 
        */
        public async Task<IHcsGetStateResult> WaitForResultAsync(
            IHcsAck ack, bool withInitialDelay = false, CancellationToken token = default)
        {
            IHcsGetStateResult result;
            for (int attempts = 1; ; attempts++) {

                if (attempts > 1 || withInitialDelay) {
                    _config.Log($"Ожидаю {_config.ResultWaitingDelayMillis}мс до (следующей) попытки получить ответ...");
                    await Task.Delay(_config.ResultWaitingDelayMillis, token);
                }

                _config.Log($"Запрашиваю ответ, попытка #{attempts}...");
                result = await TryGetResultAsync(ack);
                if (result != null) break;
            }

            _config.Log($"Ответ получен, число частей: {result.Items.Count()}");
            return result;
        }
    }
}
