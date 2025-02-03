
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
    public class HcsDebtResponseImporter: HcsDebtRequestsMethod
    {
        public HcsDebtResponseImporter(HcsClientConfig config) : base(config)
        {
        }

        public async Task<HcsDebtResponseResult[]> ImportDSRResponses(
            HcsDebtResponse[] debtResponses, CancellationToken token = default)
        {
            if (debtResponses == null || debtResponses.Length == 0)
                throw new ArgumentException("Пустой debtResponses");

            var actions = debtResponses.Select(x => ConvertToImportAction(x)).ToArray();

            var requestHeader = CreateRequestHeader();
            var requestBody = new DebtRequests.importDSRResponsesRequest {
                Id = HcsConstants.SignedXmlElementId,
                // версия предустановлена в WSDL, реальная версия шаблонов дает ошибку "Bad Request"
                //version = HcsConstants.DefaultHCSVersionString, 
                action = actions
            };

            var request = new DebtRequests.importResponsesRequest {
                RequestHeader = requestHeader,
                importDSRResponsesRequest = requestBody
            };

            var ack = await SendAsync(request, token);
            var result = await WaitForResultAsync(ack, true, token);

            var responseResults = result.Items.Select(
                x => ParseDebtResponseResultSafely(x)).ToArray();

            if (debtResponses.Length != responseResults.Length)
                throw new HcsException(
                    $"Количество направленных ответов {debtResponses.Length} не совпадает" +
                    $" с количеством {responseResults.Length} результатов обработки");

            // каждому результату ответа указываем гуид подзапроса, к которому он относится
            foreach(var response in debtResponses) {
                var found = responseResults.FirstOrDefault(x => x.TransportGuid == response.TransportGuid);
                if (found != null) found.SubrequestGuid = response.SubrequestGuid;
            }

            return responseResults;
        }

        private DebtRequests.importDSRResponsesRequestAction ConvertToImportAction(
            HcsDebtResponse source)
        {
            DebtRequests.DebtInfoType[] debtInfo = null;
            if (source.HasDebt) {
                if (IsArrayEmpty(source.PersonalData)) throw new HcsException("Не указаны должники");
                debtInfo = source.PersonalData.Select(x => new DebtRequests.DebtInfoType {
                    person = new DebtRequests.DebtInfoTypePerson {
                        firstName = x.FirstName,
                        lastName = x.LastName,
                        middleName = x.MiddleName
                    }
                }).ToArray();
            }

            var responseData = new DebtRequests.ImportDSRResponseType() {
                hasDebt = source.HasDebt,
                description = source.Description,
                Items = debtInfo,
                //debtInfo = debtInfo, // was in hcs-v13
                executorGUID = ClientConfig.ExecutorGUID
            };

            return new DebtRequests.importDSRResponsesRequestAction() {
                subrequestGUID = source.SubrequestGuid.ToString(), // идентификатор ИХ запроса к НАМ
                TransportGUID = source.TransportGuid.ToString(), // идентификатор НАШЕГО ответа ИМ
                actionType = DebtRequests.DSRResponseActionType.Send,
                responseData = responseData
            };
        }

        private HcsDebtResponseResult ParseDebtResponseResultSafely(object resultItem)
        {
            try {
                return ParseDebtResponseResult(resultItem);
            }
            catch (Exception e) {
                return new HcsDebtResponseResult() { Error = e };
            }
        }

        private HcsDebtResponseResult ParseDebtResponseResult(object resultItem)
        {
            if (resultItem == null) throw new HcsException("Пустой resultItem");

            var common = resultItem as DebtRequests.CommonResultType;
            if (common == null) throw new HcsException($"Неожиданный тип экземпляра ответа {resultItem.GetType()}");

            if (common.Items == null || common.Items.Length == 0)
                throw new HcsException("Пустой набор common.Items");

            var result = new HcsDebtResponseResult();
            foreach (var commonItem in common.Items) {
                if (commonItem == null) throw new HcsException("Пустой commonItem");

                switch (commonItem) {
                    case DebtRequests.CommonResultTypeError error:
                        result.Error = new HcsRemoteException(error.ErrorCode, error.Description);
                        break;
                    case DateTime updateDate:
                        result.UpdateDate = updateDate;
                        break;
                    default:
                        throw new HcsException($"Неожиданный тип сommonItem" + commonItem.GetType());
                }
            }

            result.TransportGuid = ParseGuid(common.TransportGUID);
            return result;
        }
    }
}
