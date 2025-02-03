
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// переключение на версию 13.2.3.3 (13.12.2023)
//using DebtRequests = Hcs.Service.Async.DebtRequests.v13_2_3_3;
// переключение на версию 14.0.0.0 (23.01.2024)
//using DebtRequests = Hcs.Service.Async.DebtRequests.v14_0_0_0;
// переключение на версию 14.1.0.0 (12.03.2024)
//using DebtRequests = Hcs.Service.Async.DebtRequests.v14_1_0_0;
// переключение на версию 14.5.0.1 (11.09.2024)
using DebtRequests = Hcs.Service.Async.DebtRequests.v14_5_0_1;

namespace Hcs.ClientApi.DebtRequestsApi
{
    /// <summary>
    /// Метод получения данных о направленных нам (под)запросах о наличии задолженности.
    /// </summary>
    public class HcsDebtSubrequestExporter : HcsDebtRequestsMethod
    {
        public HcsDebtSubrequestExporter(HcsClientConfig config) : base(config)
        {
            EnableMinimalResponseWaitDelay = true;
        }

        public class DSRsBatch
        {
            public List<HcsDebtSubrequest> DebtSubrequests = new List<HcsDebtSubrequest>();
            public Guid NextSubrequestGuid;
            public bool LastPage;
        }

        public async Task<HcsDebtSubrequest> ExportDSRByRequestNumber(string requestNumber, CancellationToken token)
        {
            var conditionTypes = new List<DebtRequests.ItemsChoiceType5>();
            var conditionValues = new List<object>();

            conditionTypes.Add(DebtRequests.ItemsChoiceType5.requestNumber);
            conditionValues.Add(requestNumber);

            var result = await ExportSubrequestBatchByCondition(
                conditionTypes.ToArray(), conditionValues.ToArray(), token);

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
            bool firstGuidIsReliable = false;

            while (true) {
                if (numResults == 0) Log("Запрашиваем первую партию записей...");
                else Log($"Запрашиваем следующую партию записей, уже получено {numResults}...");

                var batch = await ExportDSRsBatchByPeriodOfSending(
                    startDate, endDate, nextSubrequestGuid, token, firstGuidIsReliable);

                foreach (var s in batch.DebtSubrequests) {
                    if (resultHandler != null) resultHandler(s);
                    numResults += 1;
                }

                if (batch.LastPage) break;
                nextSubrequestGuid = batch.NextSubrequestGuid;
                firstGuidIsReliable = true;
            }

            return numResults;
        }

        public async Task<DSRsBatch> ExportDSRsBatchByPeriodOfSending(
            DateTime startDate, DateTime endDate, Guid? firstSubrequestGuid,
            CancellationToken token, bool firstGuidIsReliable)
        {
            var conditionTypes = new List<DebtRequests.ItemsChoiceType5>();
            var conditionValues = new List<object>();

            // условием на выборку запросов делаем период направления
            conditionTypes.Add(DebtRequests.ItemsChoiceType5.periodOfSendingRequest);
            conditionValues.Add(new DebtRequests.Period() { startDate = startDate, endDate = endDate });

            //  если указан ГУИД начального запроса, добавляем его в условия
            if (firstSubrequestGuid != null) {
                conditionTypes.Add(DebtRequests.ItemsChoiceType5.exportSubrequestGUID);
                conditionValues.Add(firstSubrequestGuid.ToString());
            }

            Func<Task<DSRsBatch>> taskFunc = async ()
                => await ExportSubrequestBatchByCondition(
                        conditionTypes.ToArray(), conditionValues.ToArray(), token);

            Func<Exception, bool> canIgnoreFunc = delegate (Exception e) {
                return CanIgnoreSuchException(e, firstGuidIsReliable);
            };

            return await RunRepeatableTaskAsync(taskFunc, canIgnoreFunc, int.MaxValue);
        }

        private async Task<DSRsBatch> ExportSubrequestBatchByCondition(
            DebtRequests.ItemsChoiceType5[] conditionTypes, object[] conditionValues,
            CancellationToken token)
        {
            var requestHeader = CreateRequestHeader();
            var requestBody = new DebtRequests.exportDSRsRequest {
                Id = HcsConstants.SignedXmlElementId,
                //version = "13.1.10.1",
                version = "14.0.0.0", // переход на v14 (19.01.2024)
                ItemsElementName = conditionTypes,
                Items = conditionValues
            };

            var request = new DebtRequests.exportDebtSubrequestsRequest {
                RequestHeader = requestHeader,
                exportDSRsRequest = requestBody
            };

            var ack = await SendAsync(request, token);

            try {
                var result = await WaitForResultAsync(ack, true, token);
                return ParseExportResultBatch(result);
            }
            catch (HcsNoResultsRemoteException) {
                // если нет результатов для экспорта, то возвращаем пустой пакет
                return new DSRsBatch() { LastPage = true };
            }
        }

        private DSRsBatch ParseExportResultBatch(RemoteCaller.IHcsGetStateResult result)
        {
            var batch = new DSRsBatch();

            result.Items.OfType<DebtRequests.exportDSRsResultType>().ToList().ForEach(r => {
                Log($"Принято запросов о наличии задолженности: {r.subrequestData?.Count()}");

                // на последней странице вывода может не быть ни одной записи
                if (r.subrequestData != null) {
                    r.subrequestData.ToList().ForEach(s => { batch.DebtSubrequests.Add(Adapt(s)); });
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

        private HcsDebtSubrequest Adapt(DebtRequests.DSRType s)
        {
            var dsr = new HcsDebtSubrequest();
            dsr.SubrequestGuid = ParseGuid(s.subrequestGUID);
            dsr.RequestGuid = ParseGuid(s.requestInfo.requestGUID);
            dsr.RequestNumber = s.requestInfo.requestNumber;
            dsr.SentDate = s.requestInfo.sentDate;
            dsr.Address = s.requestInfo.housingFundObject.address;

            var hfo = s.requestInfo.housingFundObject;
            if (hfo.Items != null &&
                hfo.ItemsElementName != null &&
                hfo.Items.Length == hfo.ItemsElementName.Length) {

                for (int i = 0; i < hfo.Items.Length; i++) {
                    string itemValue = hfo.Items[i];
                    switch (hfo.ItemsElementName[i]) {
                        case DebtRequests.ItemsChoiceType7.HMobjectGUID:
                            dsr.HМObjectGuid = ParseGuid(itemValue);
                            break;
                        case DebtRequests.ItemsChoiceType7.houseGUID:
                            dsr.GisHouseGuid = ParseGuid(itemValue);
                            break;
                        case DebtRequests.ItemsChoiceType7.adressType:
                            dsr.HMObjectType = itemValue;
                            break;
                        case DebtRequests.ItemsChoiceType7.addressDetails:
                            dsr.AddressDetails = itemValue;
                            break;
                    }
                }
            }

            // ГУИД здания в ФИАС может быть не указан
            if (!string.IsNullOrEmpty(hfo.fiasHouseGUID)) {
                dsr.FiasHouseGuid = ParseGuid(hfo.fiasHouseGUID);
            }

            // from hcs-v13
            //dsr.GisHouseGuid = ParseGuid(s.requestInfo.housingFundObject.houseGUID);
            //dsr.AddressDetails = s.requestInfo.housingFundObject.addressDetails;

            dsr.DebtStartDate = s.requestInfo.period.startDate;
            dsr.DebtEndDate = s.requestInfo.period.endDate;
            dsr.ResponseStatus = ConvertStatusType(s.responseStatus);
            dsr.ResponseDate = s.requestInfo.responseDate;

            return dsr;
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

        private bool CanIgnoreSuchException(Exception e, bool firstGuidIsReliable)
        {
            // "Произошла ошибка при передаче данных. Попробуйте осуществить передачу данных повторно."
            if (HcsUtil.EnumerateInnerExceptions(e).Any(
                x => x is HcsRemoteException && (x as HcsRemoteException).ErrorCode == "EXP001000")) {
                return true;
            }

            // Возникающий на больших списках отказ возобновляемый, учитывем факт что GUID был
            // получен из ГИСЖКХ и явно является надежным.
            if (firstGuidIsReliable && HcsUtil.EnumerateInnerExceptions(e).Any(
                x => x.Message != null && x.Message.Contains("Error loading content: Content not found for guid:"))) {
                return true;
            }

            return false;
        }
    }
}
