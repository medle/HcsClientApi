
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
    /// Метод добавления/изменения/удаления элементов списка адресных объектов 
    /// в договоре ресурсоснабжения.
    /// </summary>
    public class HcsMethodImportSupplyResourceContractObjectAddress : HcsHouseManagementMethod
    {
        public HcsMethodImportSupplyResourceContractObjectAddress
            (HcsClientConfig config) : base(config)
        {
            // запрос возвращает мало данных и исполняется быстро, можно меньше ждать
            EnableMinimalResponseWaitDelay = true;

            // этот метод можно исполнять многократно
            CanBeRestarted = true;
        }

        public async Task ImportObjectAddresses(
            ГисДоговор договор, 
            IEnumerable<ГисАдресныйОбъект> адресаДляРазмещения, 
            IEnumerable<ГисАдресныйОбъект> адресаДляУдаления,
            CancellationToken token)
        {
            if (договор == null) throw new ArgumentNullException(nameof(договор));

            var list = new List<HouseManagement.importSupplyResourceContractObjectAddressRequestObjectAddress>();
            if (адресаДляРазмещения != null) list.AddRange(адресаДляРазмещения.Select(AdoptForLoading));
            if (адресаДляУдаления != null) list.AddRange(адресаДляУдаления.Select(AdoptForRemoval));

            var request = new HouseManagement.importSupplyResourceContractObjectAddressRequest() {
                Id = HcsConstants.SignedXmlElementId,
                Item = FormatGuid(договор.ГуидДоговора),
                ItemElementName = HouseManagement.ItemChoiceType28.ContractRootGUID,
                ObjectAddress = list.ToArray()
                //version = "13.1.1.1" // версия указана в API 
            };

            var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                var ackResponse = await portClient.importSupplyResourceContractObjectAddressDataAsync(
                    CreateRequestHeader(), request);
                return ackResponse.AckRequest.Ack;
            }, token);

            // ожидаем столько ответов сколько было команд в запросе
            ParseImportResults(stateResult, list.Count(), true);
        }

        /// <summary>
        /// Готовит структуру адресного объекта для удаления в ГИС.
        /// </summary>
        private HouseManagement.importSupplyResourceContractObjectAddressRequestObjectAddress AdoptForRemoval(
            ГисАдресныйОбъект адрес)
        {
            if (адрес == null) throw new ArgumentNullException(nameof(адрес));

            // один транспортный GUID для одного адреса
            Guid transportGuid = Guid.NewGuid();

            bool deleteObject = true;

            return new HouseManagement.importSupplyResourceContractObjectAddressRequestObjectAddress() {
                TransportGUID = FormatGuid(transportGuid),
                ObjectGUID = FormatGuid(адрес.ГуидАдресногоОбъекта),
                Item = deleteObject
            };
        }

        /// <summary>
        /// Готовит структуру адресного объекта для добавления/обновления в ГИС.
        /// </summary>
        private HouseManagement.importSupplyResourceContractObjectAddressRequestObjectAddress AdoptForLoading(
            ГисАдресныйОбъект адрес)
        {
            if (адрес == null) throw new ArgumentNullException(nameof(адрес));

            // один транспортный GUID для одного адреса
            Guid transportGuid = Guid.NewGuid();

            var serviceType = new HouseManagement.ContractSubjectObjectAdressTypeServiceType() {
                Code = HcsHouseManagementNsi.ElectricSupplyServiceType.Code,
                GUID = HcsHouseManagementNsi.ElectricSupplyServiceType.GUID,
                Name = HcsHouseManagementNsi.ElectricSupplyServiceType.Name
            };

            var municipalResource = new HouseManagement.ContractSubjectObjectAdressTypeMunicipalResource() {
                Code = HcsHouseManagementNsi.ElectricSupplyMunicipalResource.Code,
                GUID = HcsHouseManagementNsi.ElectricSupplyMunicipalResource.GUID,
                Name = HcsHouseManagementNsi.ElectricSupplyMunicipalResource.Name
            };

            // сведения о поставляемом ресурсе
            var pair = new HouseManagement.importSupplyResourceContractObjectAddressRequestObjectAddressLoadObjectPair() {
                TransportGUID = FormatGuid(Guid.NewGuid()), // получал BadRequest пока не сделал здесь новый GUID
                ServiceType = serviceType,
                MunicipalResource = municipalResource,
                StartSupplyDate = DateTime.Now // в договоре нет даты начала снабжения адреса, ставлю что-нибудь
            };

            var loadObject = new HouseManagement.importSupplyResourceContractObjectAddressRequestObjectAddressLoadObject() {
                FIASHouseGuid = FormatGuid(адрес.ГуидЗданияФиас),
                ApartmentNumber = MakeEmptyNull(адрес.НомерПомещения), // строка длинной 0 дает "Bad request"
                RoomNumber = MakeEmptyNull(адрес.НомерКомнаты),
                Pair = [pair]
            };

            var address = new HouseManagement.importSupplyResourceContractObjectAddressRequestObjectAddress() {
                TransportGUID = FormatGuid(transportGuid),
                Item = loadObject
            };

            // для обновления известного адресного объекта указываем его ГУИД
            if (адрес.ГуидАдресногоОбъекта != default) {
                address.ObjectGUID = FormatGuid(адрес.ГуидАдресногоОбъекта);
            }

            return address;
        }
    }
}
