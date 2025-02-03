using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hcs.ClientApi.DeviceMeteringApi
{
    public class HcsDeviceMeteringUtil
    {
        public static string ConvertMeterReading(string reading, bool isRequired)
        {
            if (string.IsNullOrEmpty(reading)) return (isRequired ? "0" : null);

            // исрравляем типичный отказ ГИС в приеме показаний: заменяем запятую на точку
            string betterReading = reading.Contains(",") ? reading.Replace(",", ".") : reading;

            // Шаблон из: http://open-gkh.ru/MeteringDeviceBase/MeteringValueType.html
            var match = Regex.Match(betterReading, "^\\d{1,15}(\\.\\d{1,7})?$");
            if (match.Success) return betterReading;

            throw new HcsException($"Значение показания \"{reading}\" не соответствует требованиям ГИС: N.N");
        }
    }
}
