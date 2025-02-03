using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using Hcs.ClientApi.RemoteCaller;
using Hcs.ClientApi.DataTypes;

//using HouseManagement = Hcs.Service.Async.HouseManagement.v13_2_3_3;
// переключение на версию 14.1.0.0 (12.03.2024)
//using HouseManagement = Hcs.Service.Async.HouseManagement.v14_1_0_0;
// переключение на версию 14.5.0.1 (11.09.2024)
using HouseManagement = Hcs.Service.Async.HouseManagement.v14_5_0_1;

namespace Hcs.ClientApi.HouseManagementApi
{
    /// <summary>
    /// Метод получения списка адресных объектов по договору ресурсоснабжения.
    /// </summary>
    public class HcsMethodExportSupplyResourceContractObjectAddress : HcsHouseManagementMethod
    {
        public HcsMethodExportSupplyResourceContractObjectAddress(HcsClientConfig config) : base(config)
        {
            // запрос возвращает мало данных и исполняется быстро, можно меньше ждать
            EnableMinimalResponseWaitDelay = true;
            CanBeRestarted = true;
        }

        /// <summary>
        /// Запрос на экспорт объектов жилищного фонда из договора ресурсоснабжения.
        /// </summary>
        public async Task<int> QueryAddresses(
            ГисДоговор договор, Action<ГисАдресныйОбъект> resultHandler, CancellationToken token)
        {
            int numResults = 0;

            Action<ГисАдресныйОбъект> countingHandler = (result) => {
                numResults += 1;
                resultHandler(result);
            };

            Guid? nextGuid = null;
            while (true) {
                var paged = await QueryOneBatch(договор, countingHandler, nextGuid, token);
                if (paged.IsLastPage) break;
                nextGuid = paged.NextGuid;
                numResults += 1;
            }

            return numResults;
        }

        private async Task<HcsPagedResultState> QueryOneBatch(
            ГисДоговор договор, Action<ГисАдресныйОбъект> resultHandler, 
            Guid? firstGuid, CancellationToken token)
        {
            // имена и значения параметров запроса
            var itemNames = new List<HouseManagement.ItemsChoiceType29> { };
            List<string> items = new List<string> { };

            // в параметр нельзя поместить более одного договора
            if (договор.ГуидВерсииДоговора != default) {
                // запрашиваем адреса явной версии договора если она есть
                itemNames.Add(HouseManagement.ItemsChoiceType29.ContractGUID);
                items.Add(FormatGuid(договор.ГуидВерсииДоговора));
            }
            else { // иначе главный ГУИД договора
                itemNames.Add(HouseManagement.ItemsChoiceType29.ContractRootGUID);
                items.Add(FormatGuid(договор.ГуидДоговора));
            }

            // если указан guid следующей страницы данных, добавляем его в параметры,
            // (на 20.12.2023 эта функция не работает, первый пакет содержит 1000 записей
            // и запрос второго пакета с ExportObjectGUID возвращает "Bad request")
            if (firstGuid != null) {
                itemNames.Add(HouseManagement.ItemsChoiceType29.ExportObjectGUID);
                items.Add(FormatGuid(firstGuid));
            }

            var request = new HouseManagement.exportSupplyResourceContractObjectAddressRequest {
                Id = HcsConstants.SignedXmlElementId,
                Items = items.ToArray(),
                ItemsElementName = itemNames.ToArray(),
                version = "13.1.1.1" // номер версии из сообщения об ошибке сервера HCS
            };

            try {
                var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                    var ackResponse = await portClient.exportSupplyResourceContractObjectAddressDataAsync(
                        CreateRequestHeader(), request);
                    return ackResponse.AckRequest.Ack;
                }, token);

                var result = RequireSingleItem
                    <HouseManagement.getStateResultExportSupplyResourceContractObjectAddress>(stateResult.Items);
                foreach (var x in result.ObjectAddress) {
                    resultHandler(Adopt(x));
                }

                return new HcsPagedResultState(result.Item);
            }
            catch (HcsNoResultsRemoteException) {
                // допускаем отсутствие результатов
                return HcsPagedResultState.IsLastPageResultState;
            }
        }

        private ГисАдресныйОбъект Adopt(
            HouseManagement.exportSupplyResourceContractObjectAddressResultType source)
        {
            return new ГисАдресныйОбъект() {
                ТипЗдания = (source.HouseTypeSpecified ? source.HouseType.ToString() : null),
                ГуидЗданияФиас = ParseGuid(source.FIASHouseGuid),
                ГуидДоговора = ParseGuid(source.ContractRootGUID),
                ГуидВерсииДоговора = ParseGuid(source.ContractGUID),
                ГуидАдресногоОбъекта = ParseGuid(source.ObjectGUID),
                НомерПомещения = source.ApartmentNumber,
                НомерКомнаты = source.RoomNumber
            };
        }
    }
}
