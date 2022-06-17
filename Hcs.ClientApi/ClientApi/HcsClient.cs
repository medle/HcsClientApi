
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

using Hcs.ClientApi.Config;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Единый клиент для вызова всех реализованных функций интеграции с ГИС ЖКХ.
    /// </summary>
    public class HcsClient : HcsClientConfig
    {
        public HcsClient()
        {
            HcsServicePointConfig.InitConfig();

            // роль поставщика информации по умолчанию
            Role = HcsOrganizationRoles.RSO;
        }

        public void SetSigningCertificate(X509Certificate2 cert, string pin = null)
        {
            if (cert == null) throw new ArgumentException("Null certificate");
            if (pin == null) pin = HcsConstants.DefaultCertificatePin;

            CertificateThumbprint = cert.Thumbprint;
            CertificatePassword = pin;
            CryptoProviderType = cert.GetProviderType();
        }

        public async Task<HcsDebtSubrequest> ExportDSRByRequestNumber(string requestNumber)
        {
            var worker = new HcsDebtSubrequestExporter(this);
            return await worker.ExportDSRByRequestNumber(requestNumber);
        }

        /// <summary>
        /// Получение списка запросов о наличии задолженности направленных в данный период.
        /// </summary>
        public async Task<int> ExportDSRsByPeriodOfSending(
            DateTime startDate, 
            DateTime endDate, 
            Action<HcsDebtSubrequest> resultHandler,
            CancellationToken token = default)
        {
            var worker = new HcsDebtSubrequestExporter(this);
            return await worker.ExportDSRsByPeriodOfSending(startDate, endDate, resultHandler, token);
        }

        /// <summary>
        /// Отправка пакета ответов на запросы о наличии задолженности.
        /// </summary>
        public async Task<int> ImportDSRsResponsesAsOneBatch(
            HcsDebtResponse[] responses,
            Action<HcsDebtResponse, HcsDebtResponseResult> resultHandler,
            CancellationToken token = default)
        {
            var worker = new HcsDebtResponseImporter(this);
            var results = await worker.ImportDSRResponses(responses, token);

            // в пакете результатов надо найти результат по каждому ответу
            foreach (var response in responses) {
                var result = results.FirstOrDefault(
                    x => x.SubrequestGuid == response.SubrequestGuid && 
                         x.TransportGuid == response.TransportGuid);

                if (result == null) {
                    result = new HcsDebtResponseResult();
                    result.TransportGuid = response.TransportGuid;
                    result.SubrequestGuid = response.SubrequestGuid;
                    result.Error = new HcsException(
                        $"В пакете результатов приема ответов нет" +
                        $" результата для подзапроса {response.SubrequestGuid}");
                }

                // выполняем обработчик пользователя
                resultHandler(response, result);
            }

            return responses.Length;
        }

        /// <summary>
        /// Отправка ответов на запросы о наличии задолженности для списков любой длины.
        /// </summary>
        public async Task<int> ImportDSRsResponses(
            HcsDebtResponse[] responses, 
            Action<HcsDebtResponse, HcsDebtResponseResult> resultHandler, 
            CancellationToken token = default)
        {
            // разбиваем массив ответов на части ограниченной длины
            int chunkSize = 20;
            int i = 0;
            HcsDebtResponse[][] chunks = 
                responses.GroupBy(s => i++ / chunkSize).Select(g => g.ToArray()).ToArray();

            int n = 0;
            foreach (var chunk in chunks) {
                n += await ImportDSRsResponsesAsOneBatch(chunk, resultHandler, token);
            }

            return n;
        }

        /// <summary>
        /// Отправка ответа на один запрос о наличии задолженности.
        /// </summary>
        public async Task<HcsDebtResponseResult> ImportDSRResponse(
            HcsDebtResponse response, CancellationToken token = default)
        {
            HcsDebtResponse[] array = { response };
            HcsDebtResponseResult result = null;
            await ImportDSRsResponses(array, (x, y) => result = y, token);
            return result;
        }

        /// <summary>
        /// Пример получения данных об одном здании по его идентификатору в ФИАС.
        /// </summary>
        public async Task<string> ExportHouseByFiasGuid(Guid fiasHouseGuid)
        {
            var worker = new HcsHouseExporter(this);
            return await worker.ExportHouseByFiasGuid(fiasHouseGuid);
        }

        public X509Certificate2 FindCertificate(Func<X509Certificate2, bool> predicate)
        {
            return HcsCertificateHelper.FindCertificate(predicate);
        }

        public X509Certificate2 ShowCertificateUI()
        {
            return HcsCertificateHelper.ShowCertificateUI();
        }
    }
}
