
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
    public class HcsDebtResponseImporter: HcsWorkerBase
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

            var requestHeader = HcsRequestHelper.CreateHeader<DebtRequests.RequestHeader>(ClientConfig);
            var requestBody = new DebtRequests.importDSRResponsesRequest {
                Id = HcsConstants.SignElementId,
                // версия предустановлена в WSDL, реальная версия шаблонов дает ошибку "Bad Request"
                //version = HcsConstants.DefaultHCSVersionString, 
                action = actions
            };

            var request = new DebtRequests.importResponsesRequest {
                RequestHeader = requestHeader,
                importDSRResponsesRequest = requestBody
            };

            var provider = new DebtRequestsProvider(ClientConfig);
            var ack = await provider.SendAsync(request);
            var result = await provider.WaitForResultAsync(ack, true, token);

            if (result.Items == null) throw new HcsException("Пустой result.Items");
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
                debtInfo = debtInfo,
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
