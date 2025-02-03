
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
    /// Метод получения реестра договоров ресурсоснабжения (ДРСО).
    /// </summary>
    public class HcsMethodExportSupplyResourceContractData : HcsHouseManagementMethod
    {
        public HcsMethodExportSupplyResourceContractData(HcsClientConfig config) : base(config)
        {
            CanBeRestarted = true;
        }

        /// <summary>
        /// Получает один договор ресурсоснабжения по его GUID.
        /// </summary>
        public async Task<ГисДоговор> QueryOne(Guid contractRootGuid, CancellationToken token)
        {
            ГисДоговор договор = null;
            Action<ГисДоговор> handler = (result) => { договор = result; };
            await QueryOneBatch(contractRootGuid, null, handler, null, token);
            if (договор == null) 
                throw new HcsNoResultsRemoteException($"Нет договора РСО с ГУИД {contractRootGuid}");
            return договор;
        }

        /// <summary>
        /// Получает один договор ресурсоснабжения по его номеру договора.
        /// </summary>
        public async Task<ГисДоговор[]> QueryByContractNumber(string contractNumber, CancellationToken token)
        {
            var list = new List<ГисДоговор>();
            Action<ГисДоговор> handler = list.Add;
            await QueryOneBatch(null, contractNumber, handler, null, token);
            if (!list.Any()) throw new HcsNoResultsRemoteException($"Нет договора РСО с номером {contractNumber}");
            return list.ToArray();
        }

        /// <summary>
        /// Получает полный список реестра договоров ресурсоснабжения.
        /// </summary>
        public async Task<int> QueryAll(Action<ГисДоговор> resultHandler, CancellationToken token)
        {
            int numResults = 0;
            int numPages = 0;

            Action<ГисДоговор> countingHandler = (result) => {
                numResults += 1;
                resultHandler(result);
            };

            Guid? nextGuid = null;
            while (true) {
                if (++numPages > 1) Log($"Запрашиваем страницу #{numPages} данных...");
                var paged = await QueryOneBatch(null, null, countingHandler, nextGuid, token);
                if (paged.IsLastPage) break;
                nextGuid = paged.NextGuid;
            }

            return numResults;
        }

        private async Task<HcsPagedResultState> QueryOneBatch(
            Guid? contractRootGuid, string contractNumber, Action<ГисДоговор> resultHandler,
            Guid? exportNextGuid, CancellationToken token)
        {
            // все договоры можно получить многостраничной выдачей без параметров
            var itemNames = new List<HouseManagement.ItemsChoiceType27> { };
            List<object> items = new List<object> { };

            // если указан гуид договора для получения
            if (contractRootGuid != null) {
                itemNames.Add(HouseManagement.ItemsChoiceType27.ContractRootGUID);
                items.Add(FormatGuid(contractRootGuid));
            }

            // если указан номер договора для получения
            if (contractNumber != null) {
                itemNames.Add(HouseManagement.ItemsChoiceType27.ContractNumber);
                items.Add(contractNumber);
            }

            // если указан guid следующей страницы данных, добавляем его в параметры
            if (exportNextGuid != null) {
                itemNames.Add(HouseManagement.ItemsChoiceType27.ExportContractRootGUID);
                items.Add(FormatGuid(exportNextGuid));            
            }

            var request = new HouseManagement.exportSupplyResourceContractRequest {
                Id = HcsConstants.SignedXmlElementId,
                Items = items.ToArray(),
                ItemsElementName = itemNames.ToArray(),
                version = "13.1.1.1" // значение из сообщения об ошибке от сервера HCS
            };

            var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                    var ackResponse = await portClient.exportSupplyResourceContractDataAsync(
                        CreateRequestHeader(), request);
                    return ackResponse.AckRequest.Ack;
                }, token);

            var result = RequireSingleItem
                <HouseManagement.getStateResultExportSupplyResourceContractResult>(stateResult.Items);

            foreach (var c in result.Contract) {
                resultHandler(Adopt(c));
            }

            return new HcsPagedResultState(result.Item);
        }

        private ГисДоговор Adopt(HouseManagement.exportSupplyResourceContractResultType source)
        {
            var договор = new ГисДоговор() {
                ГуидДоговора = ParseGuid(source.ContractRootGUID),
                ГуидВерсииДоговора = ParseGuid(source.ContractGUID),
                НомерВерсии = source.VersionNumber,
                СостояниеДоговора = Adopt(source.ContractState),
                СтатусВерсииДоговора = Adopt(source.VersionStatus)
            };

            if(source.Item is HouseManagement.ExportSupplyResourceContractTypeIsContract isContract) { 
                договор.ТипДоговораРСО = ГисТипДоговораРСО.НеПубличныйИлиНеНежилые;
                договор.НомерДоговора = isContract.ContractNumber;
                договор.ДатаЗаключения = (DateTime?)isContract.SigningDate;
                договор.ДатаВступленияВСилу = (DateTime?)isContract.EffectiveDate;
                if (isContract.ContractAttachment != null) {
                    договор.ПриложенияДоговора = isContract.ContractAttachment.Select(AdoptAttachment).ToArray();
                }
            }

            if (source.Item is HouseManagement.ExportSupplyResourceContractTypeIsNotContract isNotContract) {
                договор.ТипДоговораРСО = ГисТипДоговораРСО.ПубличныйИлиНежилые;
                договор.НомерДоговора = isNotContract.ContractNumber;
                договор.ДатаЗаключения = isNotContract.SigningDateSpecified ? isNotContract.SigningDate : null;
                договор.ДатаВступленияВСилу = isNotContract.EffectiveDateSpecified ? isNotContract.EffectiveDate : null;
                if (isNotContract.ContractAttachment != null) {
                    договор.ПриложенияДоговора = isNotContract.ContractAttachment.Select(AdoptAttachment).ToArray();
                }
            }

            var предметы = new List<ГисПредметДоговора>();
            foreach (var subject in source.ContractSubject) {
                var предмет = new ГисПредметДоговора() {
                    КодНсиУслуги = subject.ServiceType.Code,
                    ГуидНсиУслуги = ParseGuid(subject.ServiceType.GUID),
                    ИмяНсиУслуги = subject.ServiceType.Name,
                    КодНсиРесурса = subject.MunicipalResource.Code,
                    ГуидНсиРесурса = ParseGuid(subject.MunicipalResource.GUID),
                    ИмяНсиРесурса = subject.MunicipalResource.Name
                };
                предметы.Add(предмет);
            }
            договор.ПредметыДоговора = предметы.ToArray();

            договор.Контрагент = AdoptCounterparty(source.Item1);

            if (source.CountingResourceSpecified) {
                if (source.CountingResource == HouseManagement.ExportSupplyResourceContractTypeCountingResource.R)
                    договор.НачисленияРазмещаетРСО = true;
            }

            if (source.MeteringDeviceInformationSpecified) {
                if (source.MeteringDeviceInformation == true)
                    договор.ПриборыРазмещаетРСО = true;
            }

            return договор;
        }

        private ГисПриложение AdoptAttachment(HouseManagement.AttachmentType attachment)
        {
            return new ГисПриложение() {
                ИмяПриложения = attachment.Name,
                ГуидПриложения = ParseGuid(attachment.Attachment.AttachmentGUID),
                ХэшПриложения = attachment.AttachmentHASH
            };
        }

        /// <summary>
        /// Разбор сведений о контрагенте - второй стороне договора.
        /// </summary>
        private ГисКонтрагент AdoptCounterparty(object item1)
        {
            switch (item1) {

                // вледелец помещения
                case HouseManagement.ExportSupplyResourceContractTypeApartmentBuildingOwner owner:
                    return AdoptCounterpartyEntity(owner.Item, ГисТипКонтрагента.ВладелецПомещения);

                case HouseManagement.ExportSupplyResourceContractTypeApartmentBuildingRepresentativeOwner rep:
                    return AdoptCounterpartyEntity(rep.Item, ГисТипКонтрагента.ВладелецПомещения);

                case HouseManagement.ExportSupplyResourceContractTypeApartmentBuildingSoleOwner sole:
                    return AdoptCounterpartyEntity(sole.Item, ГисТипКонтрагента.ВладелецПомещения);

                case HouseManagement.ExportSupplyResourceContractTypeLivingHouseOwner owner:
                    return AdoptCounterpartyEntity(owner.Item, ГисТипКонтрагента.ВладелецПомещения);

                // управляющая компания
                case HouseManagement.ExportSupplyResourceContractTypeOrganization uk:
                    return new ГисКонтрагент() { 
                        ТипКонтрагента = ГисТипКонтрагента.УправляющаяКомпания,
                        ГуидОрганизации = ParseGuid(uk.orgRootEntityGUID) 
                    };
            }

            return new ГисКонтрагент() { ТипКонтрагента = ГисТипКонтрагента.НеУказано };
        }

        /// <summary>
        /// Разбор ссылки на контрагента - второй стороны договора.
        /// </summary>
        private ГисКонтрагент AdoptCounterpartyEntity(object item, ГисТипКонтрагента типКонтрагента)
        {
            switch (item) {

                case HouseManagement.DRSORegOrgType org:
                    return new ГисКонтрагент() { 
                        ТипКонтрагента = типКонтрагента,
                        ГуидОрганизации = ParseGuid(org.orgRootEntityGUID) 
                    };

                case HouseManagement.DRSOIndType ind:
                    var индивид = new ГисИндивид() {
                        Фамилия = ind.Surname,
                        Имя = ind.FirstName,
                        Отчество = ind.Patronymic
                    };

                    switch (ind.Item) {
                        case string снилс: индивид.СНИЛС = снилс; break;
                        case HouseManagement.ID id:
                            индивид.НомерДокумента = id.Number;
                            индивид.СерияДокумента = id.Series;
                            индивид.ДатаДокумента = id.IssueDate;
                            break;
                    }

                    return new ГисКонтрагент() { ТипКонтрагента = типКонтрагента, Индивид = индивид,  };
            }

            return new ГисКонтрагент() { ТипКонтрагента = ГисТипКонтрагента.НеУказано };
        }

        internal static ГисСтатусВерсииДоговора Adopt(
            HouseManagement.exportSupplyResourceContractResultTypeVersionStatus source)
        {
            switch (source) {
                case HouseManagement.exportSupplyResourceContractResultTypeVersionStatus.Posted: return ГисСтатусВерсииДоговора.Размещен;
                case HouseManagement.exportSupplyResourceContractResultTypeVersionStatus.Terminated: return ГисСтатусВерсииДоговора.Расторгнут;
                case HouseManagement.exportSupplyResourceContractResultTypeVersionStatus.Draft: return ГисСтатусВерсииДоговора.Проект;
                case HouseManagement.exportSupplyResourceContractResultTypeVersionStatus.Annul: return ГисСтатусВерсииДоговора.Аннулирован;
                default: throw NewUnexpectedObjectException(source);
            }
        }

        internal static ГисСостояниеДоговора Adopt(
            HouseManagement.exportSupplyResourceContractResultTypeContractState source)
        {
            switch (source) {
                case HouseManagement.exportSupplyResourceContractResultTypeContractState.Expired: return ГисСостояниеДоговора.ИстекСрокДействия;
                case HouseManagement.exportSupplyResourceContractResultTypeContractState.NotTakeEffect: return ГисСостояниеДоговора.НеВступилВСилу;
                case HouseManagement.exportSupplyResourceContractResultTypeContractState.Proceed: return ГисСостояниеДоговора.Действующий;
                default: throw NewUnexpectedObjectException(source);
            }
        }
    }
}
