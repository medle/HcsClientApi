
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hcs.ClientApi.DebtRequestsApi
{
    public class HcsDebtRequestsApi
    {
        private HcsClientConfig config;

        public HcsDebtRequestsApi(HcsClientConfig config)
        {
            this.config = config;
        }

        public async Task<HcsDebtSubrequest> ExportDSRByRequestNumber(
            string requestNumber, CancellationToken token = default)
        {
            var worker = new HcsDebtSubrequestExporter(config);
            return await worker.ExportDSRByRequestNumber(requestNumber, token);
        }

        /// <summary>
        /// Получение списка запросов о наличии задолженности направленных в данный период.
        /// </summary>
        public async Task<int> ExportDSRsByPeriodOfSending(
            DateTime startDate,
            DateTime endDate,
            Guid? firstSubrequestGuid,
            Action<HcsDebtSubrequest> resultHandler,
            CancellationToken token = default)
        {
            var worker = new HcsDebtSubrequestExporter(config);
            return await worker.ExportDSRsByPeriodOfSending(
                startDate, endDate, firstSubrequestGuid, resultHandler, token);
        }

        /// <summary>
        /// Отправка пакета ответов на запросы о наличии задолженности.
        /// </summary>
        public async Task<int> ImportDSRsResponsesAsOneBatch(
            HcsDebtResponse[] responses,
            Action<HcsDebtResponse, HcsDebtResponseResult> resultHandler,
            CancellationToken token = default)
        {
            var worker = new HcsDebtResponseImporter(config);
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
    }
}
