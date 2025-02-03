
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GostCryptography.Gost_28147_89;
using Hcs.ClientApi.DataTypes;
using Hcs.Service.Async.HouseManagement.v14_5_0_1;


//using HouseManagement = Hcs.Service.Async.HouseManagement.v13_2_3_3;
// переключение на версию 14.1.0.0 (12.03.2024)
//using HouseManagement = Hcs.Service.Async.HouseManagement.v14_1_0_0;
// переключение на версию 14.5.0.1 (11.09.2024)
using HouseManagement = Hcs.Service.Async.HouseManagement.v14_5_0_1;

namespace Hcs.ClientApi.HouseManagementApi
{
    /// <summary>
    /// Метод получения списка приборов учета.
    /// </summary>
    public class HcsMethodExportMeteringDeviceData : HcsHouseManagementMethod
    {
        public HcsMethodExportMeteringDeviceData(HcsClientConfig config) : base(config)
        {
            CanBeRestarted = true;
        }

        /// <summary>
        /// Получение списка приборов учета для одного здания.
        /// </summary>
        public async Task<int> ExportByHouse(
            Guid fiasHouseGuid, Action<ГисПриборУчета> resultHandler, CancellationToken token)
        {
            List<HouseManagement.ItemsChoiceType4> itemNames = [HouseManagement.ItemsChoiceType4.FIASHouseGuid];
            List<string> items = [FormatGuid(fiasHouseGuid)];

            var request = new HouseManagement.exportMeteringDeviceDataRequest {
                Id = HcsConstants.SignedXmlElementId,
                Items = items.ToArray(),
                ItemsElementName = itemNames.ToArray(),
                //version = "11.1.0.2" // версия указана в API
            };

            int numResults = 0;
            try {
                var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                    var ackResponse = await portClient.exportMeteringDeviceDataAsync(CreateRequestHeader(), request);
                    return ackResponse.AckRequest.Ack;
                }, token);

                stateResult.Items.OfType<HouseManagement.exportMeteringDeviceDataResultType>().ToList().ForEach(
                    device => { resultHandler(Adopt(device)); numResults += 1; }
                    );
            }
            catch (HcsNoResultsRemoteException) {
                // допускаем отсутствие результатов
                return 0;
            }

            return numResults;
        }

        private ГисПриборУчета Adopt(HouseManagement.exportMeteringDeviceDataResultType source)
        {
            var прибор = new ГисПриборУчета() {
                ГуидПрибораУчета = ParseGuid(source.MeteringDeviceRootGUID),
                ГуидВерсииПрибора = ParseGuid(source.MeteringDeviceVersionGUID),
                НомерПрибораУчетаГис = source.MeteringDeviceGISGKHNumber,
                ЗаводскойНомер = source.BasicChatacteristicts.MeteringDeviceNumber,
                МодельПрибораУчета = source.BasicChatacteristicts.MeteringDeviceModel,
                ДатаРазмещенияВерсии = source.UpdateDateTime
            };

            // гуид организации владеющей прибором учета на законном основании
            if (!IsArrayEmpty(source.MeteringOwner)) {
                прибор.ГуидВладельцаПрибора = ParseGuid(source.MeteringOwner[0]);
            }

            // статус прибора учета
            switch (source.StatusRootDoc) {
                case HouseManagement.exportMeteringDeviceDataResultTypeStatusRootDoc.Active:
                    прибор.СтатусПрибораУчета = ГисСтатусПрибораУчета.Активный;
                    break;
                case HouseManagement.exportMeteringDeviceDataResultTypeStatusRootDoc.Archival:
                    прибор.СтатусПрибораУчета = ГисСтатусПрибораУчета.Архивный;
                    break;
                default: 
                    throw new HcsException($"Неизвестный статус ПУ {source.StatusRootDoc} для №{прибор.ЗаводскойНомер}"); 
            }

            var basic = source.BasicChatacteristicts;

            прибор.ДатаУстановки = basic.InstallationDateSpecified ? basic.InstallationDate : null;
            прибор.ДатаВводаВЭксплуатацию = basic.CommissioningDateSpecified ? basic.CommissioningDate : null;
            прибор.ДатаПоследнейПоверки = basic.FirstVerificationDateSpecified ? basic.FirstVerificationDate : null;
            прибор.ДатаИзготовления = basic.FactorySealDateSpecified ? basic.FactorySealDate : null;

            прибор.РежимДистанционногоОпроса = basic.RemoteMeteringMode;
            прибор.ОписаниеДистанционногоОпроса = basic.RemoteMeteringInfo;

            object basicItem = basic.Item;
            bool типНайден = false;

            CallOnType<HouseManagement.MeteringDeviceBasicCharacteristicsTypeResidentialPremiseDevice>(basicItem, x => {
                прибор.ГуидыЛицевыхСчетов = ParseGuidArray(x.AccountGUID);
                прибор.ГуидыПомещений = ParseGuidArray(x.PremiseGUID);
                прибор.ВидПрибораУчета = ГисВидПрибораУчета.ЖилоеПомещение;
                типНайден = true;
            });

            CallOnType<HouseManagement.MeteringDeviceBasicCharacteristicsTypeNonResidentialPremiseDevice>(basicItem, x => {
                прибор.ГуидыЛицевыхСчетов = ParseGuidArray(x.AccountGUID);
                прибор.ГуидыПомещений = ParseGuidArray(x.PremiseGUID);
                прибор.ВидПрибораУчета = ГисВидПрибораУчета.НежилоеПомещение;
                типНайден = true;
            });

            CallOnType<HouseManagement.MeteringDeviceBasicCharacteristicsTypeCollectiveDevice>(basicItem, x => {
                прибор.ГуидыЗданийФиас = ParseGuidArray(x.FIASHouseGuid);
                прибор.ВидПрибораУчета = ГисВидПрибораУчета.ОДПУ;
                типНайден = true;
            });

            CallOnType<HouseManagement.MeteringDeviceBasicCharacteristicsTypeCollectiveApartmentDevice>(basicItem, x => {
                прибор.ГуидыЛицевыхСчетов = ParseGuidArray(x.AccountGUID);
                прибор.ГуидыПомещений = ParseGuidArray(x.PremiseGUID);
                прибор.ВидПрибораУчета = ГисВидПрибораУчета.КоммунальнаяКвартира;
                типНайден = true;
            });

            CallOnType<HouseManagement.MeteringDeviceBasicCharacteristicsTypeLivingRoomDevice>(basicItem, x => {
                прибор.ГуидыЛицевыхСчетов = ParseGuidArray(x.AccountGUID);
                прибор.ГуидыЖилыхКомнат = ParseGuidArray(x.LivingRoomGUID);
                прибор.ВидПрибораУчета = ГисВидПрибораУчета.ЖилаяКомната;
                типНайден = true;
            });

            CallOnType<HouseManagement.MeteringDeviceBasicCharacteristicsTypeApartmentHouseDevice>(basicItem, x => {
                прибор.ГуидыЗданийФиас = ParseGuidArray(x.FIASHouseGuid);
                прибор.ГуидыЛицевыхСчетов = ParseGuidArray(x.AccountGUID);
                прибор.ВидПрибораУчета = ГисВидПрибораУчета.ЖилойДом;
                типНайден = true;
            });

            if (!типНайден) throw new HcsException($"Неизвестный тип ПУ {basicItem} для №{прибор.ЗаводскойНомер}");

            foreach (var electric in source.Items.OfType<MunicipalResourceElectricExportType>())
            {
                прибор.КоэффициентТрансформации = 
                    (electric.TransformationRatioSpecified ? electric.TransformationRatio : 0);
                прибор.ПоказаниеТ1 = electric.MeteringValueT1;
                прибор.ПоказаниеТ2 = electric.MeteringValueT2;
                прибор.ПоказаниеТ3 = electric.MeteringValueT3;
            }

            return прибор;
        }
    }
}
