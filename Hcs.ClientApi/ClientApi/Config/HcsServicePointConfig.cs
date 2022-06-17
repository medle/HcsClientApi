using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Hcs.GostXades;

namespace Hcs.ClientApi.Config
{
    /// <summary>
    /// Конфигурация ServicePointManager для работы с tls
    /// </summary>
    public static class HcsServicePointConfig
    {
        /// <summary>
        /// Первичная инициализация
        /// </summary>
        public static void InitConfig()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.CheckCertificateRevocationList = false;
            ServicePointManager.ServerCertificateValidationCallback = ((sender, certificate, chain, sslPolicyErrors) => true);
            ServicePointManager.Expect100Continue = false;
        }

    }
}
