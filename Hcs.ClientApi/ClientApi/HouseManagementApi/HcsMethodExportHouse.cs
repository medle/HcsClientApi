
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
    /// Метод получения информации о доме и его помещениях.
    /// </summary>
    public class HcsMethodExportHouse : HcsHouseManagementMethod
    {
        public HcsMethodExportHouse(HcsClientConfig config) : base(config)
        {
            CanBeRestarted = true;
        }

        public async Task<ГисЗдание> ExportHouseByFiasGuid(Guid fiasHouseGuid, CancellationToken token)
        {
            var request = new HouseManagement.exportHouseRequest {
                Id = HcsConstants.SignedXmlElementId,
                FIASHouseGuid = FormatGuid(fiasHouseGuid), 
                version = "12.2.0.1" // это значение из сообщения об ошибке если указать "13.1.10.1"
            };

            var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                var response = await portClient.exportHouseDataAsync(CreateRequestHeader(), request);
                return response.AckRequest.Ack;
            }, token);

            return Adopt(RequireSingleItem<HouseManagement.exportHouseResultType>(stateResult.Items));
        }

        private ГисЗдание Adopt(HouseManagement.exportHouseResultType source)
        {
            bool заполнен = false;

            var дом = new ГисЗдание();
            дом.НомерДомаГис = source.HouseUniqueNumber;
            var помещения = new List<ГисПомещение>();

            var apartmentHouse = source.Item as HouseManagement.exportHouseResultTypeApartmentHouse;
            if (apartmentHouse != null) {
                дом.ТипДома = ГисТипДома.Многоквартирный;
                дом.ГуидЗданияФиас = ParseGuid(apartmentHouse.BasicCharacteristicts.FIASHouseGuid);
                if (apartmentHouse.ResidentialPremises != null) {
                    apartmentHouse.ResidentialPremises.ToList().ForEach(x => помещения.Add(Adopt(x)));
                }
                if (apartmentHouse.NonResidentialPremises != null) {
                    apartmentHouse.NonResidentialPremises.ToList().ForEach(x => помещения.Add(Adopt(x)));
                }
                заполнен = true;
            }

            var livingHouse = source.Item as HouseManagement.exportHouseResultTypeLivingHouse;
            if (livingHouse != null) {
                дом.ТипДома = ГисТипДома.Жилой;
                дом.ГуидЗданияФиас = ParseGuid(livingHouse.BasicCharacteristicts.FIASHouseGuid);
                заполнен = true;
            }

            if (!заполнен) throw new HcsException("В информации о доме неизвестный тип данных: " + source.Item);

            дом.Помещения = помещения.ToArray();
            return дом;
        }

        private ГисПомещение Adopt(HouseManagement.exportHouseResultTypeApartmentHouseResidentialPremises source)
        {
            return new ГисПомещение() {
                ЭтоЖилоеПомещение = true,
                НомерПомещения = source.PremisesNum,
                ГуидПомещения = ParseGuid(source.PremisesGUID),
                ДатаПрекращения = source.TerminationDateSpecified ? source.TerminationDate : null,
                Аннулирование = source.AnnulmentInfo
            };
        }

        private ГисПомещение Adopt(HouseManagement.exportHouseResultTypeApartmentHouseNonResidentialPremises source)
        {
            return new ГисПомещение() {
                ЭтоЖилоеПомещение = false,
                НомерПомещения = source.PremisesNum,
                ГуидПомещения = ParseGuid(source.PremisesGUID)
            };
        }
    }
}
