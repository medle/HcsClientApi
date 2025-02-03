
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hcs.ClientApi.DataTypes;

namespace Hcs.ClientApi.DeviceMeteringApi
{
    /// <summary>
    /// Методы ГИС ЖКХ сервиса hcs-device-metering (показания приборов учета) 
    /// </summary>
    public class HcsDeviceMeteringApi
    {
        public HcsClientConfig Config { get; private set; }

        public HcsDeviceMeteringApi(HcsClientConfig config)
        {
            this.Config = config;
        }

        public async Task<DateTime> РазместитьПоказания(
            ГисПриборУчета прибор, ГисПоказания показания, CancellationToken token = default)
        {
            var method = new HcsMethodImportMeteringDevicesValues(Config);
            return await method.ImportMeteringDevicesValues(прибор, показания, token);
        }
    }
}
