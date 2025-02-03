
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.DataTypes;
using HouseManagement = Hcs.Service.Async.HouseManagement.v14_5_0_1;

namespace Hcs.ClientApi.HouseManagementApi
{
    /// <summary>
    /// Операции размещения и закрытия Лицевых счетов в ГИС ЖКХ.
    /// </summary>
    public class HcsMethodImportAccountData : HcsHouseManagementMethod
    {
        public HcsMethodImportAccountData(HcsClientConfig config) : base(config)
        {
            // этот метод нельзя исполнять многократно
            CanBeRestarted = false;
        }

        /// <summary>
        /// Размещение нового Лицевого счета если ГисЛицевойСчет.ГуидЛицевогоСчета не заполнен, 
        /// размещение новой версии лицевого счета если заполнен.
        /// Возвращает Единый номер лицевого счета в ГИС ЖКХ для размещенного ЛС.
        /// http://open-gkh.ru/HouseManagement/importAccountRequest/Account.html
        /// </summary>
        public async Task<string> ImportAccount(
            ГисДоговор договор, ГисЛицевойСчет лицевойСчет, CancellationToken token)
        {
            if (лицевойСчет == null) throw new ArgumentNullException(nameof(лицевойСчет));
            if (договор == null) throw new ArgumentNullException(nameof(договор));

            var account = ConvertToAccount(договор, лицевойСчет);
            var result = await CallImportAccountData(account, token);
            return result.UnifiedAccountNumber;
        }

        private HouseManagement.importAccountRequestAccount ConvertToAccount(
            ГисДоговор договор, ГисЛицевойСчет лицевойСчет)
        {
            var account = new HouseManagement.importAccountRequestAccount() { 
                TransportGUID = FormatGuid(Guid.NewGuid()),
                AccountNumber = лицевойСчет.НомерЛицевогоСчета
            };

            if (лицевойСчет.ГуидЛицевогоСчета != default) { 
                account.AccountGUID = FormatGuid(лицевойСчет.ГуидЛицевогоСчета);
            }

            // собираем указание на договор в рамках которого открыт ЛС
            if (договор.ГуидДоговора == null) throw new HcsException("Не указан ГуидДоговора для размещения ЛС");
            var reasonRSO = new HouseManagement.AccountReasonsImportTypeSupplyResourceContract() { 
                Items = [ FormatGuid(договор.ГуидДоговора) ],
                ItemsElementName = [ HouseManagement.ItemsChoiceType9.ContractGUID ]
            };
            account.AccountReasons = new HouseManagement.AccountReasonsImportType() {
                SupplyResourceContract = [ reasonRSO ]
            };

            // это лицевой счет ресурсоснабжающей организации
            account.ItemElementName = HouseManagement.ItemChoiceType18.isRSOAccount;
            account.Item = true;

            // собираем указание на помещение в котором размещается ЛС
            if (IsArrayEmpty(лицевойСчет.Размещения))
                throw new HcsException($"Не указаны размещения ЛС №{лицевойСчет.НомерЛицевогоСчета}");
            account.Accommodation = лицевойСчет.Размещения.Select(ConvertToAccomodation).ToArray();

            // если указана дата закрытия ЛС
            if (лицевойСчет.ДатаЗакрытия != null) {
                account.Closed = new HouseManagement.ClosedAccountAttributesType() {
                    CloseDate = (DateTime)лицевойСчет.ДатаЗакрытия,
                    CloseReason = HcsHouseManagementNsi.ПричинаЗакрытияЛицевогоСчета.РасторжениеДоговора
                };
            }

            // информация о плательщике
            account.PayerInfo = new HouseManagement.AccountTypePayerInfo() {
                Item = ConvertToAccountContragent(договор.Контрагент)
            };

            return account;
        }

        private object ConvertToAccountContragent(ГисКонтрагент контрагент)
        {
            if (контрагент == null) throw new HcsException("В договоре не заполнен Контрагент");

            if (контрагент.ГуидОрганизации != null) {
                if (контрагент.ГуидВерсииОрганизации == null)
                    throw new HcsException("Для размещения ЛС в договоре с ЮЛ обязательно указание ГисКонтрагент.ГуидВерсииОрганизации");
                return new HouseManagement.RegOrgVersionType() {
                    orgVersionGUID = FormatGuid(контрагент.ГуидВерсииОрганизации)
                };
            }

            if (контрагент.Индивид != null) {
                контрагент.Индивид.ПроверитьЗаполнениеСНИЛС();
                контрагент.Индивид.ПроверитьЗаполнениеФИО();

                return new HouseManagement.AccountIndType() {
                    FirstName = контрагент.Индивид.Имя,
                    Patronymic = контрагент.Индивид.Отчество,
                    Surname = контрагент.Индивид.Фамилия,
                    Item = контрагент.Индивид.СНИЛСТолькоЦифры
                };
            }

            throw new HcsException("Не указана ни организация ни индивид для размещения ЛС");
        }

        private HouseManagement.AccountTypeAccommodation ConvertToAccomodation(ГисРазмещениеЛС размещение)
        {
            if (размещение == null) throw new HcsException("Пустое размещение для ЛС");

            var accomodation = new HouseManagement.AccountTypeAccommodation();
            if (размещение.ГуидПомещения != null) {
                accomodation.ItemElementName = HouseManagement.ItemChoiceType19.PremisesGUID;
                accomodation.Item = FormatGuid(размещение.ГуидПомещения);
            }
            else if (размещение.ГуидЖилойКомнаты != null) {
                accomodation.ItemElementName = HouseManagement.ItemChoiceType19.LivingRoomGUID;
                accomodation.Item = FormatGuid(размещение.ГуидЖилойКомнаты);
            }
            else {
                throw new HcsException("Не указан ГУИД помещения или комнаты для ЛС");
            }

            if (размещение.ПроцентДоли != null) {
                accomodation.SharePercent = (decimal)размещение.ПроцентДоли;
                accomodation.SharePercentSpecified = true;
            }

            return accomodation;
        }

        private async Task<(string UnifiedAccountNumber, DateTime UpdateDate)> CallImportAccountData(
            HouseManagement.importAccountRequestAccount account,
            CancellationToken token)
        {
            var request = new HouseManagement.importAccountRequest {
                Id = HcsConstants.SignedXmlElementId,
                Account = [account]
                //version = "13.1.1.1" // версия указана в API 
            };

            var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                var ackResponse = await portClient.importAccountDataAsync(
                    CreateRequestHeader(), request);
                return ackResponse.AckRequest.Ack;
            }, token);

            var commonResult = ParseSingleImportResult(stateResult);

            // получаем результат
            switch (commonResult.ItemElementName) {

                case HouseManagement.ItemChoiceType2.ImportAccount:
                    var accountResult = RequireType<HouseManagement.getStateResultImportResultCommonResultImportAccount>(commonResult.Item);

                    DateTime updateDate = commonResult.Items.OfType<DateTime>().FirstOrDefault();
                    if (updateDate == default) 
                        throw new HcsException("В ответе сервера не указана дата обновления лицевого счета");

                    return (accountResult.UnifiedAccountNumber, updateDate);

                default:
                    throw new HcsException($"Неожиданная структура в пакете результата: {commonResult.ItemElementName}");
            }
        }
    }
}
