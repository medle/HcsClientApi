
namespace Hcs.ClientApi.Config 
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
        /// Устаноавливаемый пользователем приемник отладочных сообщений.
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

        /// <summary>
        /// Число миллисекунд которое следует ждать между попытками получить ответ на запрос.
        /// </summary>
        public int ResultWaitingDelayMillis { get; set; } = 5000;
    }
}
