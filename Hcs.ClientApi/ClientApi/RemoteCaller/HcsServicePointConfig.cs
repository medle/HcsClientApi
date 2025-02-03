using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Hcs.GostXades;

namespace Hcs.ClientApi.RemoteCaller
{
    /// <summary>
    /// Конфигурация ServicePointManager для работы с TLS. Скорее всего класс не нужен.
    /// </summary>
    public static class HcsServicePointConfig
    {
        public static void InitConfig()
        {
            // отключено 15.12.2023, работает и так
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            //ServicePointManager.CheckCertificateRevocationList = false;
            //ServicePointManager.ServerCertificateValidationCallback = ((sender, certificate, chain, sslPolicyErrors) => true);
            //ServicePointManager.Expect100Continue = false;
        }

    }
}
