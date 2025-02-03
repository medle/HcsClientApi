
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
    /// Метод передачи в ГИС ЖКХ сведений о договоре РСО (новом или уже существующем).
    /// </summary>
    public class HcsMethodImportSupplyResourceContractData : HcsHouseManagementMethod
    {
        public HcsMethodImportSupplyResourceContractData(HcsClientConfig config) : base(config)
        {
            // этот метод нельзя исполнять многократно
            CanBeRestarted = false;
        }

        /// <summary>
        /// Размещение нового договора если ГисДоговор.ГуидДоговора не заполнен, 
        /// размещение новой версии договора если заполнен.
        /// http://open-gkh.ru/HouseManagement/SupplyResourceContractType.html
        /// </summary>
        public async Task<DateTime> ImportContract(
            ГисДоговор договор, IEnumerable<ГисАдресныйОбъект> адреса, CancellationToken token)
        {
            if (договор == null) throw new ArgumentNullException(nameof(договор));
            if (адреса == null || !адреса.Any())
                throw new ArgumentException($"Для импорта нового договора {договор.НомерДоговора}" + 
                    " необходимо указать хотя-бы один адресный объект");

            // регистрация нового договора выполняется с пустым гуид договора
            Guid? contractGuid = (договор.ГуидДоговора == default) ? null : договор.ГуидДоговора;
            var contract = ConvertToSupplyResourceContract(договор, адреса);
            return await CallImportContract(contractGuid, contract, token);
        }

        /// <summary>
        /// Вызывает удаленный метод импорта договора с @contractGuid и данными операции импорта @contractItem.
        /// Чтобы перевести договор из состояния "Проект" в состояние "Размещен" необходимо вызвать
        /// importSupplyResourceContractProjectData/PlacingContractProject=true
        /// http://open-gkh.ru/HouseManagement/importSupplyResourceContractRequest.html
        /// </summary>
        private async Task<DateTime> CallImportContract(
            Guid? contractGuid, object contractItem, CancellationToken token)
        {
            var contract = new HouseManagement.importSupplyResourceContractRequestContract();
            HouseManagement.importSupplyResourceContractRequestContract[] contracts = { contract };

            // Передаем условие запроса - гуид версии договора.
            // При создании нового договора атрибут importSupplyResourceContractRequest.Contract.ContractGUID не заполняется.
            if (contractGuid != null) {
                contract.ItemElementName = HouseManagement.ItemChoiceType26.ContractRootGUID;
                contract.Item = FormatGuid(contractGuid);
            }

            // связь операции записи с данными клиента
            contract.TransportGUID = FormatGuid(Guid.NewGuid());

            // передаем данные запроса
            contract.Item1 = contractItem;

            var request = new HouseManagement.importSupplyResourceContractRequest {
                Id = HcsConstants.SignedXmlElementId,
                Contract = contracts
                //version = "13.1.1.1" // версия указана в API 
            };

            var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                var ackResponse = await portClient.importSupplyResourceContractDataAsync(
                    CreateRequestHeader(), request);
                return ackResponse.AckRequest.Ack;
            }, token);

            var commonResult = ParseSingleImportResult(stateResult);

            // получаем результат
            switch (commonResult.ItemElementName) {
                case HouseManagement.ItemChoiceType2.ImportSupplyResourceContract:
                    var contractResult = RequireType<HouseManagement.getStateResultImportResultCommonResultImportSupplyResourceContract>(commonResult.Item);
                    var датаИмпорта = RequireSingleItem<DateTime>(commonResult.Items);
                    return датаИмпорта;
                default:
                    throw new HcsException($"Неожиданная структура в пакете результата: {commonResult.ItemElementName}");
            }
        }

        /// <summary>
        /// Преобразует модель данных ГисДоговор в модель данных HouseManagement.SupplyResourceContractType.
        /// http://open-gkh.ru/HouseManagement/SupplyResourceContractType.html
        /// </summary>
        private HouseManagement.SupplyResourceContractType ConvertToSupplyResourceContract(
            ГисДоговор договор, IEnumerable<ГисАдресныйОбъект> адреса)
        {
            var contract = new HouseManagement.SupplyResourceContractType();

            // тип договора указывается в contract.Item: нежилые помещения
            if (договор.ЭтоДоговорНежилогоПомещения) {
                var isNotContract = new HouseManagement.SupplyResourceContractTypeIsNotContract();
                isNotContract.ContractNumber = договор.НомерДоговора;
                if (договор.ДатаЗаключения != null) 
                    isNotContract.SigningDate = (DateTime)договор.ДатаЗаключения;
                if (!IsArrayEmpty(договор.ПриложенияДоговора))
                    isNotContract.ContractAttachment = договор.ПриложенияДоговора.Select(ConvertToAttachment).ToArray();
                contract.Item = isNotContract;
            }

            // тип договора указывается в contract.Item: ИКУ/СОИ/НСУ
            if (договор.ЭтоДоговорИКУ) {
                var isContract = new HouseManagement.SupplyResourceContractTypeIsContract();
                isContract.ContractNumber = договор.НомерДоговора;
                var нужнаДатаЗаключения = (договор.ДатаЗаключения != null) ? (DateTime)договор.ДатаЗаключения : DateTime.Now;
                isContract.SigningDate = isContract.EffectiveDate = нужнаДатаЗаключения;

                // для ИКУ обязательно приложение файла договора (иначе 400 Bad request)
                if (IsArrayEmpty(договор.ПриложенияДоговора))
                    throw new HcsException($"Для размещения договора ИКУ {договор.НомерДоговора} необходимо указать файл приложения");
                isContract.ContractAttachment = договор.ПриложенияДоговора.Select(ConvertToAttachment).ToArray();
                contract.Item = isContract;
            }
            
            if (договор.Контрагент == null)
                throw new HcsException($"В договоре {договор.НомерДоговора} не указан Контрагент");

            // // вторая сторона договора указывается в Item1: договор на нежилые помещения
            if (договор.ЭтоДоговорНежилогоПомещения) {

                // ApartmentBuildingOwner: Собственник или пользователь жилого (нежилого) помещения в МКД
                contract.Item1 = new HouseManagement.SupplyResourceContractTypeApartmentBuildingOwner() {
                    Item = ConvertToDRSOContragent(договор.Контрагент)
                };

                // ссылка на НСИ "Основание заключения договора" 
                contract.ContractBase = [HcsHouseManagementNsi.ОснованиеЗаключенияДоговора.ЗаявлениеПотребителя];
            }

            // вторая сторона договора указывается в Item1: договор ИКУ/СОИ/НСУ
            if (договор.ЭтоДоговорИКУ) {
                if (договор.Контрагент.ГуидОрганизации == null)
                    throw new HcsException($"В договоре ИКУ {договор.НомерДоговора} не указан ГУИД организации");

                // Organization: Управляющая организация
                contract.Item1 = new HouseManagement.SupplyResourceContractTypeOrganization() {
                    orgRootEntityGUID = FormatGuid(договор.Контрагент.ГуидОрганизации)
                };

                // ссылка на НСИ "Основание заключения договора" 
                contract.ContractBase = [HcsHouseManagementNsi.ОснованиеЗаключенияДоговора.ДоговорУправления];
            }

            // предмет договора
            Guid contractSubjectGuid = Guid.NewGuid();
            contract.ContractSubject = [
                new HouseManagement.SupplyResourceContractTypeContractSubject() {
                    ServiceType = HcsHouseManagementNsi.ElectricSupplyServiceType,
                    MunicipalResource = HcsHouseManagementNsi.ElectricSupplyMunicipalResource,
                    StartSupplyDate = (договор.ДатаЗаключения != null ? (DateTime)договор.ДатаЗаключения : DateTime.Now),
                    EndSupplyDate = DateTime.Now.AddYears(50),
                    TransportGUID = FormatGuid(contractSubjectGuid)
                }
            ];

            // порядок размещения информации о начислениях за коммунальные услуги ведется
            // "D" - в разрезе договора. "O" - в разрезе объектов. (обязательно для ИКУ)
            if (договор.ЭтоДоговорИКУ) {
                contract.AccrualProcedure = HouseManagement.SupplyResourceContractTypeAccrualProcedure.D;
                contract.AccrualProcedureSpecified = true;
            }

            if (договор.ЭтоДоговорИКУ) {

                // размещение информации о начислениях за коммунальные услуги осуществляет:
                // R(SO)- РСО. P(roprietor)-Исполнитель коммунальных услуг. (обязательно для ИКУ)
                contract.CountingResource = 
                    договор.НачисленияРазмещаетРСО ?
                    HouseManagement.SupplyResourceContractTypeCountingResource.R :
                    HouseManagement.SupplyResourceContractTypeCountingResource.P;
                contract.CountingResourceSpecified = true;

                if (договор.НачисленияРазмещаетРСО) {
                    if (договор.ПриборыРазмещаетРСО) {
                        contract.MeteringDeviceInformation = true;
                        contract.MeteringDeviceInformationSpecified = true;
                    }
                }

                // в договоре нет планового объема потребления
                contract.IsPlannedVolume = false;
            }

            if (договор.НачисленияРазмещаетРСО) {

                // Cрок предоставления платежных документов, не позднее.
                contract.BillingDate = new HouseManagement.SupplyResourceContractTypeBillingDate() {
                    Date = 15,
                    DateType = HouseManagement.SupplyResourceContractTypeBillingDateDateType.N // следующий месяц
                };

                // Срок предоставления информации о поступивших платежах, не позднее.
                contract.ProvidingInformationDate = new HouseManagement.SupplyResourceContractTypeProvidingInformationDate() {
                    Date = 15,
                    DateType = HouseManagement.SupplyResourceContractTypeProvidingInformationDateDateType.N // следующий месяц
                };
            }

            if (договор.ПриборыРазмещаетРСО) {

                // период передачи текущих показаний должен быть указан если ИПУ размещает РСО
                contract.Period = new HouseManagement.SupplyResourceContractTypePeriod() {
                    Start = new HouseManagement.SupplyResourceContractTypePeriodStart() {
                        StartDate = 1
                    },
                    End = new HouseManagement.SupplyResourceContractTypePeriodEnd() {
                        EndDate = 25
                    }
                };
            }

            // срок представления (выставления) платежных документов, не позднее. Является обязательным,
            // если вторая сторона договора отличается от "Управляющая организация"
            if (договор.ЭтоДоговорНежилогоПомещения) {
                contract.BillingDate = new HouseManagement.SupplyResourceContractTypeBillingDate() {
                    Date = -1, // последний день месяца
                    DateType = HouseManagement.SupplyResourceContractTypeBillingDateDateType.N // следующего месяца
                };

                // объем поставки определяется на основании прибора учета (признак необходим чтобы
                // ГИС разрешал размещать ПУ на лицевых счетах договора)(признак запрещен для ИКУ)
                contract.VolumeDepends = true;
                contract.VolumeDependsSpecified = true;

                // период передачи текущих показаний должен быть указан если указано VolumeDepends
                contract.Period = new HouseManagement.SupplyResourceContractTypePeriod() {
                    Start = new HouseManagement.SupplyResourceContractTypePeriodStart() {
                        StartDate = 1
                    },
                    End = new HouseManagement.SupplyResourceContractTypePeriodEnd() {
                        EndDate = 25
                    }
                };
            }

            // срок действия договора
            contract.ItemsElementName = [HouseManagement.ItemsChoiceType25.IndefiniteTerm];
            contract.Items = [ true ];

            // данные об объекте жилищного фонда. При импорте договора должен быть добавлен
            // как минимум один адрес объекта жилищного фонда
            if (адреса != null) {
                contract.ObjectAddress = адреса.Select(
                    адрес => ConvertToObjectAddress(договор, адрес, contractSubjectGuid)).ToArray();
            }

            return contract;
        }

        /// <summary>
        /// Сборка сведений для отправки указателя на файл приложения к договору.
        /// http://open-gkh.ru/Base/AttachmentType.html
        /// </summary>
        private HouseManagement.AttachmentType ConvertToAttachment(ГисПриложение приложение)
        {
            // все свойства обязательные для операции импорта договора
            return new HouseManagement.AttachmentType() {
                Name = приложение.ИмяПриложения ?? throw new HcsException("Не указано имя файла приложения"),
                Description = приложение.ОписаниеПриложения != null ? приложение.ОписаниеПриложения : приложение.ИмяПриложения,
                AttachmentHASH = приложение.ХэшПриложения ?? throw new HcsException("Не указан хэш файла приложения"),
                Attachment = new HouseManagement.Attachment() {
                    AttachmentGUID = FormatGuid(приложение.ГуидПриложения) 
                }
            };
        }

        private HouseManagement.SupplyResourceContractTypeObjectAddress ConvertToObjectAddress(
            ГисДоговор договор, ГисАдресныйОбъект адрес, Guid contractSubjectGuid)
        {
            // дату начала снабжения выводим из даты заключения договора
            DateTime startSupplyDate = (договор.ДатаЗаключения != null) ? 
                (DateTime)договор.ДатаЗаключения : DateTime.Now;

            // ссылка на пару определения ресурсов предмета договора
            var pair = new HouseManagement.SupplyResourceContractTypeObjectAddressPair();
            pair.PairKey = FormatGuid(contractSubjectGuid);
            pair.StartSupplyDate = startSupplyDate;
            pair.EndSupplyDateSpecified = false; // не указана дата окончания поставки ресурса

            var address = new HouseManagement.SupplyResourceContractTypeObjectAddress() {
                TransportGUID = FormatGuid(Guid.NewGuid()),
                FIASHouseGuid = FormatGuid(адрес.ГуидЗданияФиас),
                ApartmentNumber = MakeEmptyNull(адрес.НомерПомещения), // строка длинной 0 дает "Bad request"
                RoomNumber = MakeEmptyNull(адрес.НомерКомнаты),
                Pair = [ pair ]
            };

            if (!string.IsNullOrEmpty(адрес.ТипЗдания)) {
                address.HouseTypeSpecified = true;
                address.HouseType = ConvertToHouseType(адрес.ТипЗдания); 
            }

            return address;
        }

        private HouseManagement.ObjectAddressTypeHouseType ConvertToHouseType(string типЗдания)
        {
            return типЗдания switch {
                ГисАдресныйОбъект.ИзвестныеТипыЗдания.MKD => HouseManagement.ObjectAddressTypeHouseType.MKD,
                ГисАдресныйОбъект.ИзвестныеТипыЗдания.ZHD => HouseManagement.ObjectAddressTypeHouseType.ZHD,
                ГисАдресныйОбъект.ИзвестныеТипыЗдания.ZHDBlockZastroyki => HouseManagement.ObjectAddressTypeHouseType.ZHDBlockZastroyki,
                _ => throw new HcsException($"Указан неизвестный тип здания [{типЗдания}]")
            };
        }

        /// <summary>
        /// Преобразует реквизиты контрагента в модель данных ГИС ЖКХ
        /// </summary>
        private object ConvertToDRSOContragent(ГисКонтрагент контрагент)
        {
            if (контрагент.ГуидОрганизации != null) {
                return new HouseManagement.DRSORegOrgType() {
                    orgRootEntityGUID = FormatGuid(контрагент.ГуидОрганизации)
                };
            }

            if (контрагент.Индивид != null) {
                контрагент.Индивид.ПроверитьЗаполнениеСНИЛС();
                контрагент.Индивид.ПроверитьЗаполнениеФИО();

                return new HouseManagement.DRSOIndType() {
                    Patronymic = MakeEmptyNull(контрагент.Индивид.Отчество),
                    FirstName = MakeEmptyNull(контрагент.Индивид.Имя),
                    Surname = MakeEmptyNull(контрагент.Индивид.Фамилия),
                    Item = MakeEmptyNull(контрагент.Индивид.СНИЛСТолькоЦифры) // в СНИЛС требуется только 11 цифр
                };
            }

            // по умолчанию будет вариант bool "NoData"
            return false;
        }

        /// <summary>
        /// Выполнение операции размещения факта расторжения договора.
        /// http://open-gkh.ru/HouseManagement/importSupplyResourceContractRequest/Contract/TerminateContract.html
        /// </summary>
        public async Task<DateTime> TerminateContract(
            ГисДоговор договор, DateTime датаРасторжения, CancellationToken token)
        {
            var terminate = new HouseManagement.importSupplyResourceContractRequestContractTerminateContract();
            terminate.Terminate = датаРасторжения;
            terminate.ReasonRef = HcsHouseManagementNsi.ПричинаРасторженияДоговора.ПоВзаимномуСогласиюСторон;
            return await CallImportContract(договор.ГуидДоговора, terminate, token);
        }

        /// <summary>
        /// Выполнение операции размещения факта аннулирование договора.
        /// http://open-gkh.ru/HouseManagement/AnnulmentType.html
        /// </summary>
        public async Task<DateTime> AnnulContract(ГисДоговор договор, string причина, CancellationToken token)
        {
            var annulment = new HouseManagement.AnnulmentType();
            annulment.ReasonOfAnnulment = причина;
            return await CallImportContract(договор.ГуидДоговора, annulment, token);
        }
    }
}
