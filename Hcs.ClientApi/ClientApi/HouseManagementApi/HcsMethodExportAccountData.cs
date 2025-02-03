
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.DataTypes;

//using HouseManagement = Hcs.Service.Async.HouseManagement.v13_2_3_3;
// переключение на версию 14.1.0.0 (12.03.2024)
//using HouseManagement = Hcs.Service.Async.HouseManagement.v14_1_0_0;
// переключение на версию 14.5.0.1 (11.09.2024)
using HouseManagement = Hcs.Service.Async.HouseManagement.v14_5_0_1;

namespace Hcs.ClientApi.HouseManagementApi
{
    /// <summary>
    /// Метод получения реестра лицевых счетов.
    /// </summary>
    public class HcsMethodExportAccountData : HcsHouseManagementMethod
    {
        public HcsMethodExportAccountData(HcsClientConfig config) : base(config)
        {
            CanBeRestarted = true;
        }

        /// <summary>
        /// Получает реестр лицевых счетов по зданию с данным ГУИД ФИАС или по списку номеров ЕЛС.
        /// </summary>
        public async Task<int> Query(
            Guid? fiasHouseGuid, IEnumerable<string> unifiedAccountNumbers, 
            Action<ГисЛицевойСчет> resultHandler, CancellationToken token)
        {
            int numResults = 0;

            var itemNames = new List<HouseManagement.ItemsChoiceType18> { };
            List<string> items = new List<string> { };

            // если указано здание
            if (fiasHouseGuid != null) {
                itemNames.Add(HouseManagement.ItemsChoiceType18.FIASHouseGuid);
                items.Add(FormatGuid(fiasHouseGuid));
            }

            // если указаны ЕЛС
            if (unifiedAccountNumbers != null) {
                if (unifiedAccountNumbers.Count() > 1000) 
                    throw new HcsException($"Слишком много ЕЛС в запросе {unifiedAccountNumbers.Count()} > допустимых 1000");
                foreach (var un in unifiedAccountNumbers) {
                    itemNames.Add(HouseManagement.ItemsChoiceType18.UnifiedAccountNumber);
                    items.Add(un);
                }
            }

            try {
                var request = new HouseManagement.exportAccountRequest {
                    Id = HcsConstants.SignedXmlElementId,
                    Items = items.ToArray(),
                    ItemsElementName = itemNames.ToArray()
                };

                var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                    var ackResponse = await portClient.exportAccountDataAsync(
                        CreateRequestHeader(), request);
                    return ackResponse.AckRequest.Ack;
                }, token);

                stateResult.Items.OfType<HouseManagement.exportAccountResultType>().ToList().ForEach(
                    account => { resultHandler(Adopt(account)); numResults += 1; });
            }
            catch (HcsNoResultsRemoteException) {
                // допускаем отсутствие результатов
                return 0;
            }

            return numResults;
        }

        private ГисЛицевойСчет Adopt(HouseManagement.exportAccountResultType source)
        {
            return new ГисЛицевойСчет() {
                ГуидЛицевогоСчета = ParseGuid(source.AccountGUID),
                НомерЕЛС = source.UnifiedAccountNumber,
                НомерЛицевогоСчета = source.AccountNumber,
                ПолнаяПлощадь = (source.TotalSquareSpecified ? (decimal?)source.TotalSquare : null),
                ЖилаяПлощадь = (source.ResidentialSquareSpecified ? (decimal?)source.ResidentialSquare : null),
                КодЖКУ = source.ServiceID,
                ДатаСоздания = (source.CreationDateSpecified ? (DateTime?)source.CreationDate : null),
                ДатаЗакрытия = (source.Closed != null ? (DateTime?)source.Closed.CloseDate : null),
                КодНсиПричиныЗакрытия = (source.Closed != null ? source.Closed.CloseReason.Code : null),
                ИмяПричиныЗакрытия = (source.Closed != null ? source.Closed.Description : null),
                Размещения = Adopt(source.Accommodation),
                Основания = Adopt(source.AccountReasons)
            };
        }

        private ГисОснованиеЛС[] Adopt(HouseManagement.exportAccountResultTypeAccountReasons source)
        {
            if (source == null) throw new ArgumentNullException("HouseManagement.exportAccountResultTypeAccountReasons");

            var основания = new List<ГисОснованиеЛС>();

            // если указан договор РСО
            if (source.SupplyResourceContract != null) {
                foreach (var sr in source.SupplyResourceContract) {

                    var основание = new ГисОснованиеЛС();
                    основание.ТипОснованияЛС = ГисТипОснованияЛС.ДоговорРСО;

                    for (int i = 0; i < sr.Items.Length; i++) {
                        switch (sr.ItemsElementName[i]) {
                            case HouseManagement.ItemsChoiceType9.ContractGUID: 
                                основание.ГуидДоговора = ParseGuid(sr.Items[i]); 
                                break;
                        }
                    }

                    if (основание.ГуидДоговора == default(Guid)) 
                        throw new HcsException("Для основания ЛС не указан ГУИД договора РСО");
                    основания.Add(основание);
                }
            }

            // если указан договор соцнайма
            if (source.SocialHireContract != null) {
                var sh = source.SocialHireContract;

                var основание = new ГисОснованиеЛС();
                основание.ТипОснованияЛС = ГисТипОснованияЛС.Соцнайм;

                for (int i = 0; i < sh.Items.Length; i++) {
                    object itemValue = sh.Items[i];
                    switch (sh.ItemsElementName[i]) {
                        case HouseManagement.ItemsChoiceType10.ContractGUID:
                            основание.ГуидДоговора = ParseGuid(itemValue);
                            break;
                        case HouseManagement.ItemsChoiceType10.ContractNumber:
                            основание.НомерДоговора = (itemValue != null ? itemValue.ToString() : null);
                            break;
                    }
                }

                if (основание.ГуидДоговора == default(Guid))
                    throw new HcsException("Для основания ЛС не указан ГУИД договора соцнайма");
                основания.Add(основание);
            }

            // если указан договор с потребителем
            if (source.Contract != null) {
                var основание = new ГисОснованиеЛС();
                основание.ТипОснованияЛС = ГисТипОснованияЛС.Договор;
                основание.ГуидДоговора = ParseGuid(source.Contract.ContractGUID);
                основания.Add(основание);
            }

            // непонятно что делать с остальными типам основания и даже следует ли их
            // расшифровывать или считать ошибкой пустого списка оснований
            return основания.ToArray();
        }

        private ГисРазмещениеЛС[] Adopt(HouseManagement.AccountExportTypeAccommodation[] array)
        {
            if (array == null) throw new ArgumentNullException("HouseManagement.AccountExportTypeAccommodation");
            return array.ToList().Select(x => Adopt(x)).ToArray();
        }

        private ГисРазмещениеЛС Adopt(HouseManagement.AccountExportTypeAccommodation source)
        {
            var размещение = new ГисРазмещениеЛС();
            размещение.ПроцентДоли = (source.SharePercentSpecified ? (decimal?)source.SharePercent : null);

            switch (source.ItemElementName) {
                case HouseManagement.ItemChoiceType7.FIASHouseGuid: размещение.ГуидЗдания = ParseGuid(source.Item); break;
                case HouseManagement.ItemChoiceType7.PremisesGUID: размещение.ГуидПомещения = ParseGuid(source.Item); break;
                case HouseManagement.ItemChoiceType7.LivingRoomGUID: размещение.ГуидЖилойКомнаты = ParseGuid(source.Item); break;
                default: throw new HcsException("Неизвестный тип размещения ЛС: " + source.ItemElementName);
            }

            return размещение;
        }
    }
}
