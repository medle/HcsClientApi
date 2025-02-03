
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.DataTypes;
using Hcs.ClientApi.DeviceMeteringApi;
using HouseManagement = Hcs.Service.Async.HouseManagement.v14_5_0_1;

namespace Hcs.ClientApi.HouseManagementApi
{
    /// <summary>
    /// Метод передачи в ГИС ЖКХ сведений о приборе учета (новом или уже существующем).
    /// </summary>
    public class HcsMethodImportMeteringDeviceData : HcsHouseManagementMethod
    {
        public HcsMethodImportMeteringDeviceData(HcsClientConfig config) : base(config)
        {
            // этот метод нельзя исполнять многократно
            CanBeRestarted = false;
        }

        /// <summary>
        /// Размещение нового прибора учета если ГисПриборУчета.ГуидПрибораУчета не заполнен, 
        /// размещение новой версии прибора учета если заполнен.
        /// Возвращает GUID размещенного прибора учета.
        /// http://open-gkh.ru/HouseManagement/importMeteringDeviceDataRequest.html
        /// </summary>
        public async Task<Guid> ImportMeteringDevice(ГисПриборУчета прибор, CancellationToken token)
        {
            if (прибор == null) throw new ArgumentNullException(nameof(прибор));

            var device = ConvertToMeteringDevice(прибор);
            var result = await CallImportMeteringDevice(device, token);
            return result.MeteringDeviceGuid;
        }

        /// <summary>
        /// Выполняет архивацию(удаление) прибора учета в ГИС.
        /// В структуре ПриборУчета используется только поле ГуидВерсииПрибора.
        /// </summary>
        public async Task<DateTime> ArchiveMeteringDevice(ГисПриборУчета приборУчета, CancellationToken token)
        {
            var archive = new HouseManagement.importMeteringDeviceDataRequestMeteringDeviceDeviceDataToUpdateArchiveDevice();
            archive.ArchivingReason = HcsHouseManagementNsi.ПричинаАрхивацииПрибораУчета.ИстекСрокЭксплуатации;

            var update = new HouseManagement.importMeteringDeviceDataRequestMeteringDeviceDeviceDataToUpdate();
            update.MeteringDeviceVersionGUID = FormatGuid(приборУчета.ГуидВерсииПрибора);
            update.Item = archive;

            var device = new HouseManagement.importMeteringDeviceDataRequestMeteringDevice();
            device.TransportGUID = FormatGuid(Guid.NewGuid());
            device.Item = update;

            var result = await CallImportMeteringDevice(device, token);
            return result.UpdateDate;
        }

        private HouseManagement.importMeteringDeviceDataRequestMeteringDevice
            ConvertToMeteringDevice(ГисПриборУчета прибор)
        {
            var device = new HouseManagement.importMeteringDeviceDataRequestMeteringDevice();

            // ГИС будет возвращать ошибку с указанием этого идентификатора для определения элемента пакета
            device.TransportGUID = FormatGuid(Guid.NewGuid());

            // если заполнен ГУИД прибора обновляем старый прибор
            if (прибор.ГуидВерсииПрибора != default) {
                var update = new HouseManagement.importMeteringDeviceDataRequestMeteringDeviceDeviceDataToUpdate();
                update.MeteringDeviceVersionGUID = FormatGuid(прибор.ГуидВерсииПрибора);
                update.Item = ConvertToFullInformationType(прибор); // UpdateBeforeDevicesValues
                device.Item = update;
            }
            else { // ГУИД прибора не заполнен, добавляем новый прибор
                device.Item = ConvertToFullInformationType(прибор);
            }

            return device;
        }

        private HouseManagement.MeteringDeviceFullInformationType ConvertToFullInformationType(
            ГисПриборУчета прибор)
        {
            var basic = new HouseManagement.MeteringDeviceBasicCharacteristicsType();
            basic.MeteringDeviceNumber = прибор.ЗаводскойНомер;
            basic.MeteringDeviceModel = прибор.МодельПрибораУчета;
            basic.MeteringDeviceStamp = прибор.МодельПрибораУчета; // марку дублируем из модели

            // наличие датчиков температуры и давления
            basic.TemperatureSensor = false;
            basic.PressureSensor = false;

            basic.RemoteMeteringMode = прибор.РежимДистанционногоОпроса;
            if (прибор.РежимДистанционногоОпроса)
                basic.RemoteMeteringInfo = прибор.ОписаниеДистанционногоОпроса;

            if (прибор.ДатаУстановки != null) {
                basic.InstallationDate = (DateTime)прибор.ДатаУстановки;
                basic.InstallationDateSpecified = true;
            };

            if (прибор.ДатаВводаВЭксплуатацию != null) {
                basic.CommissioningDate = (DateTime)прибор.ДатаВводаВЭксплуатацию;
                basic.CommissioningDateSpecified = true;
            };

            if (прибор.ДатаПоследнейПоверки != null) {
                basic.FirstVerificationDate = (DateTime)прибор.ДатаПоследнейПоверки;
                basic.FirstVerificationDateSpecified = true;
            };

            if (прибор.ДатаИзготовления != null) {
                basic.FactorySealDate = (DateTime)прибор.ДатаИзготовления;
                basic.FactorySealDateSpecified = true;
            }

            switch (прибор.ВидПрибораУчета) {

                case ГисВидПрибораУчета.ОДПУ:
                    if (IsArrayEmpty(прибор.ГуидыЗданийФиас))
                        throw new HcsException("Для ОДПУ необходимо указать ГУИД здания ФИАС");
                    basic.Item = new HouseManagement.MeteringDeviceBasicCharacteristicsTypeCollectiveDevice() {
                        FIASHouseGuid = прибор.ГуидыЗданийФиас.Select(FormatGuid).ToArray()
                    };
                    break;

                case ГисВидПрибораУчета.НежилоеПомещение:
                    if (IsArrayEmpty(прибор.ГуидыЛицевыхСчетов))
                        throw new HcsException("Для размещения ПУ нежилого помещения следует указать ГУИД лицевого счета");
                    if (IsArrayEmpty(прибор.ГуидыПомещений))
                        throw new HcsException("Для размещения ПУ нежилого помещения следует указать ГУИД помещения");
                    basic.Item = new HouseManagement.MeteringDeviceBasicCharacteristicsTypeNonResidentialPremiseDevice() {
                        AccountGUID = прибор.ГуидыЛицевыхСчетов.Select(FormatGuid).ToArray(),
                        PremiseGUID = прибор.ГуидыПомещений.Select(FormatGuid).ToArray()
                    };
                    break;

                default:
                    throw new NotImplementedException(
                        "Не реализовано размещение вида прибора: " + прибор.ВидПрибораУчета);
            }

            // сведения об учете электроэнергии
            var electric = new HouseManagement.MunicipalResourceElectricBaseType();
            electric.Unit = HouseManagement.MunicipalResourceElectricBaseTypeUnit.Item245; // константа ОКЕИ 245=кВт*ч
            electric.UnitSpecified = true;
            electric.MeteringValueT1 = HcsDeviceMeteringUtil.ConvertMeterReading(прибор.ПоказаниеТ1, true);
            electric.MeteringValueT2 = HcsDeviceMeteringUtil.ConvertMeterReading(прибор.ПоказаниеТ2, false); 
            electric.MeteringValueT3 = HcsDeviceMeteringUtil.ConvertMeterReading(прибор.ПоказаниеТ3, false);
            if (прибор.КоэффициентТрансформацииУказан) {
                electric.TransformationRatio = прибор.КоэффициентТрансформации;
                electric.TransformationRatioSpecified = true;
            }

            return new HouseManagement.MeteringDeviceFullInformationType() {
                BasicChatacteristicts = basic,
                Item = true, // NotLinkedWithMetering (нет связей с другими приборами)
                Items = [electric]
            };
        }

        private async Task<(Guid MeteringDeviceGuid, DateTime UpdateDate)> CallImportMeteringDevice(
            HouseManagement.importMeteringDeviceDataRequestMeteringDevice device, 
            CancellationToken token)
        {
            HouseManagement.importMeteringDeviceDataRequestMeteringDevice[] devices = { device };

            var request = new HouseManagement.importMeteringDeviceDataRequest {
                Id = HcsConstants.SignedXmlElementId,
                MeteringDevice = devices
                //version = "13.1.1.1" // версия указана в API 
            };

            //request.FIASHouseGuid = 

            var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                var ackResponse = await portClient.importMeteringDeviceDataAsync(
                    CreateRequestHeader(), request);
                return ackResponse.AckRequest.Ack;
            }, token);

            var commonResult = ParseSingleImportResult(stateResult);

            // получаем результат
            switch (commonResult.ItemElementName) {

                case HouseManagement.ItemChoiceType2.importMeteringDevice:
                    var deviceResult = RequireType<HouseManagement.getStateResultImportResultCommonResultImportMeteringDevice>(commonResult.Item);

                    DateTime updateDate = commonResult.Items.OfType<DateTime>().FirstOrDefault();
                    if (updateDate == default) throw new HcsException("В ответе сервера не указана дата обновления прибора учета");

                    return (ParseGuid(deviceResult.MeteringDeviceGUID), updateDate);

                default:
                    throw new HcsException($"Неожиданная структура в пакете результата: {commonResult.ItemElementName}");
            }
        }
    }
}
