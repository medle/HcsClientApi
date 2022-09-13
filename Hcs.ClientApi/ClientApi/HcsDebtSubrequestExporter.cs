
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.Config;
using Hcs.ClientApi.Providers;
using DebtRequests = Hcs.Service.Async.DebtRequests.v13_1_10_1;

namespace Hcs.ClientApi
{
    public class HcsDebtSubrequestExporter: HcsWorkerBase
    {
        public HcsDebtSubrequestExporter(HcsClientConfig config): base(config)
        {
        }

        public class DSRsBatch
        {
            public List<HcsDebtSubrequest> DebtSubrequests = new List<HcsDebtSubrequest>();
            public Guid NextSubrequestGuid;
            public bool LastPage;
        }

        public async Task<HcsDebtSubrequest> ExportDSRByRequestNumber(string requestNumber)
        {
            var conditionTypes = new List<DebtRequests.ItemsChoiceType3>();
            var conditionValues = new List<object>();

            conditionTypes.Add(DebtRequests.ItemsChoiceType3.requestNumber);
            conditionValues.Add(requestNumber);

            var result = await ExportSubrequestBatchByCondition(
                conditionTypes.ToArray(), conditionValues.ToArray());

            int n = result.DebtSubrequests.Count;
            if (n == 0) return null;
            if (n == 1) return result.DebtSubrequests[0];
            throw new HcsException(
                $"По номеру запроса о наличии задолженности №{requestNumber}" +
                $" получено несколько ({n}) ответов, ожидался только один");
        }

        public async Task<int> ExportDSRsByPeriodOfSending(
            DateTime startDate, DateTime endDate, Guid? firstSubrequestGuid, 
            Action<HcsDebtSubrequest> resultHandler, CancellationToken token = default)
        {
            int numResults = 0;
            Guid? nextSubrequestGuid = firstSubrequestGuid;

            while (true) {
                if (numResults == 0) Log("Запрашиваем первую партию записей...");
                else Log($"Запрашиваем следующую партию записей, уже получено {numResults}...");

                var batch = await ExportDSRsBatchByPeriodOfSending(
                    startDate, endDate, nextSubrequestGuid, token);

                foreach (var s in batch.DebtSubrequests) {
                    if (resultHandler != null) resultHandler(s);
                    numResults += 1;    
                }

                if (batch.LastPage) break;
                nextSubrequestGuid = batch.NextSubrequestGuid;
            }

            return numResults;
        }

        public async Task<DSRsBatch> ExportDSRsBatchByPeriodOfSending(
            DateTime startDate, DateTime endDate, Guid? firstSubrequestGuid = null,
            CancellationToken token = default)
        {
            var conditionTypes = new List<DebtRequests.ItemsChoiceType3>();
            var conditionValues = new List<object>();

            // условием на выборку запросов делаем период направления
            conditionTypes.Add(DebtRequests.ItemsChoiceType3.periodOfSendingRequest);
            conditionValues.Add(new DebtRequests.Period() { startDate = startDate, endDate = endDate });

            //  если указан ГУИД начального запроса, добавляем его в условия
            if (firstSubrequestGuid != null) {
                conditionTypes.Add(DebtRequests.ItemsChoiceType3.exportSubrequestGUID);
                conditionValues.Add(firstSubrequestGuid.ToString());
            }

            Func<Task<DSRsBatch>> func = async () 
                => await ExportSubrequestBatchByCondition(
                        conditionTypes.ToArray(), conditionValues.ToArray(), token);
            return await RunRepeatableTaskAsync(func, CanWeIgnoreSuchException, 5);
        }

        private bool CanWeIgnoreSuchException(Exception e)
        {
            // Проверяем частую ошибку:
            // "Произошла ошибка при передаче данных. Попробуйте осуществить передачу данных повторно."
            if (HcsUtil.ListInnerExceptions(e).Any(
                x => x is HcsRemoteException && (x as HcsRemoteException).ErrorCode == "EXP001000")) {
                return true;
            }

            return false;
        }

        private async Task<DSRsBatch> ExportSubrequestBatchByCondition(
            DebtRequests.ItemsChoiceType3[] conditionTypes, object[] conditionValues,
            CancellationToken token = default)
        {
            var requestHeader = HcsRequestHelper.CreateHeader<DebtRequests.RequestHeader>(ClientConfig);
            var requestBody = new DebtRequests.exportDSRsRequest {
                Id = HcsConstants.SignElementId,
                version = HcsConstants.DefaultHCSVersionString,
                ItemsElementName = conditionTypes,
                Items = conditionValues
            };

            var request = new DebtRequests.exportDebtSubrequestsRequest {
                RequestHeader = requestHeader,
                exportDSRsRequest = requestBody
            };

            var provider = new DebtRequestsProvider(ClientConfig);
            var ack = await provider.SendAsync(request);
            var result = await provider.WaitForResultAsync(ack, true, token);
            var batch = new DSRsBatch();

            result.Items.OfType<DebtRequests.exportDSRsResultType>().ToList().ForEach(r => {
                Log($"Принято запросов о наличии задолженности: {r.subrequestData?.Count()}");

                if (r.subrequestData != null) {
                    r.subrequestData.ToList().ForEach(s => {

                        var dsr = new HcsDebtSubrequest();
                        dsr.SubrequestGuid = ParseGuid(s.subrequestGUID);
                        dsr.RequestGuid = ParseGuid(s.requestInfo.requestGUID);
                        dsr.RequestNumber = s.requestInfo.requestNumber;
                        dsr.SentDate = s.requestInfo.sentDate;
                        dsr.Address = s.requestInfo.housingFundObject.address;
                        // ГУИД здания в ФИАС может быть не указан
                        if (!string.IsNullOrEmpty(s.requestInfo.housingFundObject.fiasHouseGUID)) {
                            dsr.FiasHouseGuid = ParseGuid(s.requestInfo.housingFundObject.fiasHouseGUID);
                        }
                        dsr.GisHouseGuid = ParseGuid(s.requestInfo.housingFundObject.houseGUID);
                        dsr.AddressDetails = s.requestInfo.housingFundObject.addressDetails;
                        dsr.DebtStartDate = s.requestInfo.period.startDate;
                        dsr.DebtEndDate = s.requestInfo.period.endDate;
                        dsr.ResponseStatus = ConvertStatusType(s.responseStatus);
                        dsr.ResponseDate = s.requestInfo.responseDate;
                        batch.DebtSubrequests.Add(dsr);
                    });
                }

                if (r.pagedOutput == null || r.pagedOutput.Item == null) batch.LastPage = true;
                else {
                    var item = r.pagedOutput.Item;
                    if (item is bool && (bool)item == true) batch.LastPage = true;
                    else if (!Guid.TryParse(item.ToString(), out batch.NextSubrequestGuid))
                        throw new HcsException($"Неожиданное значение pagedOutput [{item}]");
                }
            });

            return batch;
        }

        private HcsDebtSubrequest.ResponseStatusType ConvertStatusType(DebtRequests.ResponseStatusType type)
        {
            switch (type) {
                case DebtRequests.ResponseStatusType.Sent: return HcsDebtSubrequest.ResponseStatusType.Sent;
                case DebtRequests.ResponseStatusType.NotSent: return HcsDebtSubrequest.ResponseStatusType.NotSent;
                case DebtRequests.ResponseStatusType.AutoGenerated: return HcsDebtSubrequest.ResponseStatusType.AutoGenerated;
                default: throw new HcsException("Неизвестный статус отправки ответа: " + type);
            }
        }
    }
}
