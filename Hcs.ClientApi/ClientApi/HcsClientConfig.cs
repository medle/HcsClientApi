
using System.Security.Cryptography.X509Certificates;
using static Org.BouncyCastle.Math.EC.ECCurve;
using System.ServiceModel;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Конфигурация клиента
    /// </summary>
    public class HcsClientConfig 
    {
        /// <summary>
        /// Идентификатор поставщика данных ГИС
        /// </summary>
        public string OrgPPAGUID { get; set; }

        /// <summary>
        /// Идентификатор организации в ГИС
        /// </summary>
        public string OrgEntityGUID { get; set; }

        /// <summary>
        /// Тип криптопровайдера полученный из сертификата.
        /// </summary>
        public Hcs.GostXades.CryptoProviderTypeEnum CryptoProviderType { get; internal set; }

        public GostCryptography.Base.ProviderType GostCryptoProviderType =>
            (GostCryptography.Base.ProviderType)CryptoProviderType;

        /// <summary>
        /// Сертификат клиента для применения при формировании запросов.
        /// </summary>
        public X509Certificate2 Certificate { get; internal set; }

        /// <summary>
        /// Отпечаток сертификата
        /// </summary>
        public string CertificateThumbprint { get; internal set; }

        /// <summary>
        /// Пароль доступа к сертификату
        /// </summary>
        public string CertificatePassword { get; internal set; }

        /// <summary>
        /// Исполнитель/сотрудник ГИСЖКХ от которого будут регистрироваться ответы.
        /// </summary>
        public string ExecutorGUID { get; set; }

        /// <summary>
        /// Признак - указывает на то, что используется внешний туннель (stunnel)
        /// </summary>
        public bool UseTunnel { get; set; }

        /// <summary>
        /// true - использовать адреса ППАК стенда иначе СИТ
        /// </summary>
        public bool IsPPAK { get; set; }

        /// <summary>
        /// Роль
        /// </summary>
        public HcsOrganizationRoles Role { get; set; }

        /// <summary>
        /// Устанавливаемый пользователем приемник отладочных сообщений.
        /// </summary>
        public IHcsLogger Logger { get; set; }

        /// <summary>
        /// Выводит сообщение в установленный приемник отладочных сообщений.
        /// </summary>
        public void Log(string message) => Logger?.WriteLine(message);

        /// <summary>
        /// Устанавливаемый пользователем механизм перехвата содержимого отправляемых 
        /// и принимаемых пакетов.
        /// </summary>
        public IHcsMessageCapture MessageCapture;

        /// <summary>
        /// Отправляет тело сообщения в установленный перехватчик.
        /// </summary>
        public void MaybeCaptureMessage(bool sent, string messageBody)
            => MessageCapture?.CaptureMessage(sent, messageBody);

        public string ComposeEndpointUri(string endpointName)
        {
            if (UseTunnel)
                return $"http://{HcsConstants.Address.UriTunnel}/{endpointName}";

            return IsPPAK ?
                      $"https://{HcsConstants.Address.UriPPAK}/{endpointName}"
                    : $"https://{HcsConstants.Address.UriSIT01}/{endpointName}";
        }
    }
}
