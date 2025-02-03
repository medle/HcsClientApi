using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.DataTypes;
using DeviceMetering = Hcs.Service.Async.DeviceMetering.v14_5_0_1;

namespace Hcs.ClientApi.DeviceMeteringApi
{
    /// <summary>
    /// Размещение в ГИС показаний прибора учета.
    /// http://open-gkh.ru/DeviceMetering/importMeteringDeviceValuesRequest.html
    /// </summary>
    public class HcsMethodImportMeteringDevicesValues : HcsDeviceMeteringMethod
    {
        public HcsMethodImportMeteringDevicesValues(HcsClientConfig config) : base(config)
        {
            // этот метод нельзя исполнять многократно
            CanBeRestarted = false;
        }

        public async Task<DateTime> ImportMeteringDevicesValues(
            ГисПриборУчета прибор, ГисПоказания показания, CancellationToken token)
        {
            if (прибор == null) throw new ArgumentNullException(nameof(прибор));
            if (показания == null) throw new ArgumentNullException(nameof(показания));

            var current = new DeviceMetering.importMeteringDeviceValuesRequestMeteringDevicesValuesElectricDeviceValueCurrentValue() {
                TransportGUID = FormatGuid(Guid.NewGuid()),
                DateValue = показания.ДатаСнятия,
                MeteringValueT1 = HcsDeviceMeteringUtil.ConvertMeterReading(показания.ПоказанияТ1, false),
                MeteringValueT2 = HcsDeviceMeteringUtil.ConvertMeterReading(показания.ПоказанияТ2, false),
                MeteringValueT3 = HcsDeviceMeteringUtil.ConvertMeterReading(показания.ПоказанияТ3, false)
            };

            var electric = new DeviceMetering.importMeteringDeviceValuesRequestMeteringDevicesValuesElectricDeviceValue() {
                CurrentValue = current
            };

            var value = new DeviceMetering.importMeteringDeviceValuesRequestMeteringDevicesValues() {
                ItemElementName = DeviceMetering.ItemChoiceType.MeteringDeviceRootGUID,
                Item = FormatGuid(прибор.ГуидПрибораУчета),
                Item1 = electric    
            };

            var request = new DeviceMetering.importMeteringDeviceValuesRequest() {
                Id = HcsConstants.SignedXmlElementId,
                MeteringDevicesValues = [ value ],
                // версия задана в API
            };

            var stateResult = await SendAndWaitResultAsync(request, async (portClient) => {
                var ackResponse = await portClient.importMeteringDeviceValuesAsync(
                    CreateRequestHeader(), request);
                return ackResponse.AckRequest.Ack;
            }, token);


            if (IsArrayEmpty(stateResult.Items)) throw new HcsException("Пустой stateResult.Items");

            stateResult.Items.OfType<DeviceMetering.CommonResultTypeError>().ToList()
                .ForEach(error => { throw HcsRemoteException.CreateNew(error.ErrorCode, error.Description); });

            var commonResult = RequireSingleItem<DeviceMetering.CommonResultType>(stateResult.Items);
            if (IsArrayEmpty(commonResult.Items)) throw new HcsException("Пустой commonResult.Items");

            DateTime датаПриема = commonResult.Items.OfType<DateTime>().FirstOrDefault();
            if (датаПриема == default) throw new HcsException("Сервер не вернул дату приема им показаний");
            return датаПриема;
        }
    }
}
