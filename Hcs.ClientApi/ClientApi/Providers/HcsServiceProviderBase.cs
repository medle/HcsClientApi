
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

            // эксперимент 19.07.2022 возникает ошибка WCF (TimeoutException 60 сек.) 
            _binding.ReceiveTimeout = TimeSpan.FromSeconds(180);
            _binding.OpenTimeout = TimeSpan.FromSeconds(180);
            _binding.SendTimeout = TimeSpan.FromSeconds(180);
            _binding.CloseTimeout = TimeSpan.FromSeconds(180);

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

                // всего нам дадут 16 попыток, ГИСЖКХ иногда реально необходимо несколько минут на исполнение запроса
                int delayMillis = 10000; // 10 секунд
                if (attempts >= 3) delayMillis *= 2;  // 20
                if (attempts >= 5) delayMillis *= 2;  // 40
                if (attempts >= 7) delayMillis *= 2;  // 80
                if (attempts >= 9) delayMillis *= 2;  // 160
                if (attempts >= 12) delayMillis *= 10; // 1600

                if (attempts > 1 || withInitialDelay) {
                    int delaySec = delayMillis / 1000;
                    _config.Log($"Ожидаю {delaySec}с. до попытки #{attempts} получить ответ...");
                    await Task.Delay(delayMillis, token);
                }

                _config.Log($"Запрашиваю ответ, попытка #{attempts}...");
                result = await TryGetResultAsync(ack);
                if (result != null) break;
            }

            _config.Log($"Ответ получен, число частей: {result.Items.Count()}");
            return result;
        }

        /// <summary>
        /// Для запросов к серверу которые можно направлять несколько раз, пять раз разрешаем
        /// серверу аномально отказаться отвечать нам с сообщением "Истекло время ожидания шлюза".
        /// Эта частая ошибка серверов ГИСЖКХ которую можно игнорировать в сценариях получения
        /// списков. В сценариях направления ответов в ГИСЖКХ может потребоваться более сложное
        /// поведение потому что повторять отправку ответов может не быть правильным.
        /// </summary>
        public async Task<T> RunRepeatableTaskInsistentlyAsync<T>(Func<Task<T>> func)
        {
            int maxAttempts = 5;
            for (int attempts = 1; ; attempts++) {
                try {
                    return await func();
                }
                catch (Exception e) {
                    string marker = "(504) Истекло время ожидания шлюза.";
                    var found = HcsUtil.ListInnerExceptions(e).Find(
                        x => x.Message != null && x.Message.Contains(marker));
                    if (found != null && attempts < maxAttempts) {
                        _config.Log($"Игнорируем #{attempts} ошибку типа [{marker}]...");
                        continue;
                    }
                    throw new HcsException("Вложенная ошибка", e);
                }
            }
        }
    }
}
