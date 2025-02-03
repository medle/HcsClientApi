
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

using Hcs.ClientApi.RemoteCaller;
using Hcs.ClientApi.DebtRequestsApi;
using Hcs.ClientApi.HouseManagementApi;
using Hcs.ClientApi.OrgRegistryCommonApi;
using Hcs.ClientApi.FileStoreServiceApi;
using GostCryptography.Gost_R3411;
using Hcs.ClientApi.DeviceMeteringApi;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Единый клиент для вызова всех реализованных функций интеграции с ГИС ЖКХ.
    /// </summary>
    public class HcsClient : HcsClientConfig
    {
        public HcsClient()
        {
            HcsServicePointConfig.InitConfig();

            // роль поставщика информации по умолчанию
            Role = HcsOrganizationRoles.RSO;
        }

        public void SetSigningCertificate(X509Certificate2 cert, string pin = null)
        {
            if (cert == null) throw new ArgumentNullException("Не указан сертификат для подписания данных");
            if (pin == null) pin = HcsConstants.DefaultCertificatePin;

            Certificate = cert;
            CertificateThumbprint = cert.Thumbprint;
            CertificatePassword = pin;
            CryptoProviderType = cert.GetProviderType();
        }

        public HcsDebtRequestsApi DebtRequests => new HcsDebtRequestsApi(this);
        public HcsHouseManagementApi HouseManagement => new HcsHouseManagementApi(this);
        public HcsOrgRegistryCommonApi OrgRegistryCommon => new HcsOrgRegistryCommonApi(this);
        public HcsFileStoreServiceApi FileStoreService => new HcsFileStoreServiceApi(this);
        public HcsDeviceMeteringApi DeviceMeteringService => new HcsDeviceMeteringApi(this);

        public X509Certificate2 FindCertificate(Func<X509Certificate2, bool> predicate)
        {
            return HcsCertificateHelper.FindCertificate(predicate);
        }

        public X509Certificate2 ShowCertificateUI()
        {
            return HcsCertificateHelper.ShowCertificateUI();
        }

        /// <summary>
        /// Производит для потока хэш по алгоритму "ГОСТ Р 34.11-94" в строке binhex. 
        /// </summary>
        public string ComputeGost94Hash(System.IO.Stream stream)
        {
            // API HouseManagement указывает, что файлы приложенные к договору должны размещаться
            // с AttachmentHASH по стандарту ГОСТ. Оказывается, ГИСЖКХ требует применения устаревшего
            // алгоритма ГОСТ Р 34.11-94 (соответствует `rhash --gost94-cryptopro file` в linux)
            using var algorithm = new Gost_R3411_94_HashAlgorithm(GostCryptoProviderType);
            var savedPosition = stream.Position;
            stream.Position = 0;
            var hashValue = HcsUtil.ConvertToHexString(algorithm.ComputeHash(stream));
            stream.Position = savedPosition;    
            return hashValue;
        }
    }
}
