
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hcs.ClientApi.RemoteCaller
{
    /// <summary>
    /// Базовый класс для методов HCS вызываемых удаленно.
    /// </summary>
    public abstract class HcsRemoteCallMethod
    {
        public HcsClientConfig _config;
        protected CustomBinding _binding;

        /// <summary>
        /// Для методов возвращающих мало данных можно попробовать сократить
        /// начальный период ожидания подготовки ответа.
        /// </summary>
        public bool EnableMinimalResponseWaitDelay { get; internal set; }

        /// <summary>
        /// Для противодействия зависанию ожидания вводится предел ожидания в минутах
        /// для методов которые можно перезапустить заново с теми-же параметрами.
        /// (с периодом в 120 минут 09.2024 не успевали за ночь получить все данные)
        /// </summary>
        public int RestartTimeoutMinutes = 20;

        /// <summary>
        /// Можно ли этот метод перезапускать в случае зависания ожидания или в случае сбоя на сервере?
        /// </summary>
        public bool CanBeRestarted { get; protected set; }

        public HcsClientConfig ClientConfig => _config;

        public HcsRemoteCallMethod(HcsClientConfig config)
        {
            this._config = config;
            ConfigureBinding();
        }

        private void ConfigureBinding()
        {
            _binding = new CustomBinding();

            // эксперимент 19.07.2022 возникает ошибка WCF (TimeoutException 60 сек.) 
            _binding.ReceiveTimeout = TimeSpan.FromSeconds(180);
            _binding.OpenTimeout = TimeSpan.FromSeconds(180);
            _binding.SendTimeout = TimeSpan.FromSeconds(180);
            _binding.CloseTimeout = TimeSpan.FromSeconds(180);

            _binding.Elements.Add(new TextMessageEncodingBindingElement {
                MessageVersion = MessageVersion.Soap11,
                WriteEncoding = Encoding.UTF8
            });

            if (_config.UseTunnel) {
                if (System.Diagnostics.Process.GetProcessesByName("stunnel").Any() ? false : true) {
                    throw new Exception("stunnel не запущен");
                }

                _binding.Elements.Add(new HttpTransportBindingElement {
                    AuthenticationScheme = (_config.IsPPAK ? System.Net.AuthenticationSchemes.Digest : System.Net.AuthenticationSchemes.Basic),
                    MaxReceivedMessageSize = int.MaxValue,
                    UseDefaultWebProxy = false
                });
            }
            else {
                _binding.Elements.Add(new HttpsTransportBindingElement {
                    AuthenticationScheme = (_config.IsPPAK ? System.Net.AuthenticationSchemes.Digest : System.Net.AuthenticationSchemes.Basic),
                    MaxReceivedMessageSize = int.MaxValue,
                    UseDefaultWebProxy = false,
                    RequireClientCertificate = true
                });
            }
        }

        protected EndpointAddress GetEndpointAddress(string endpointName)
        {
            return new EndpointAddress(_config.ComposeEndpointUri(endpointName));
        }

        protected void ConfigureEndpointCredentials(
            ServiceEndpoint serviceEndpoint, ClientCredentials clientCredentials)
        {
            serviceEndpoint.EndpointBehaviors.Add(new GostSigningEndpointBehavior(_config));

            if (!_config.IsPPAK) {
                clientCredentials.UserName.UserName = HcsConstants.UserAuth.Name;
                clientCredentials.UserName.Password = HcsConstants.UserAuth.Passwd;

                System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate (
                    object sender, X509Certificate serverCertificate, X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors) {
                        // тестовый стенд использует тестовый удостоверяющий центр, любой сертификат сервера разрешаем
                        return true;
                    };
            }
            else { // на промышленном стенде
                bool letSystemValidateServerCertificate = false;
                if (!letSystemValidateServerCertificate) {
                    System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate (
                        object sender, X509Certificate serverCertificate, X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors) {
                            // 06.06.2024 возникла ошибка "Это может быть связано с тем, что сертификат сервера
                            // не настроен должным образом с помощью HTTP.SYS в случае HTTPS."
                            // ГИС ЖКХ заменил сертификат сервера HTTPS и System.Net не смогла проверить новый.
                            // В похожем случае необходимо включить "return true" чтобы любой сертификат
                            // без проверки принимался (или найти файл lk_api_dom_gosuslugi_ru.cer нового сертификата
                            // сервера ГИС ЖКХ API в разделе "Регламенты и инструкции" портала dom.gosuslugi.ru
                            // и установить этот сертификат текущему пользователю).
                            // Файл сертификата сервера API в разделе "Регламенты и инструкции" называется, например, так:
                            // "Сертификат открытого ключа для организации защищенного TLS соединения с сервисами
                            // легковесной интеграции (c 10.06.2024)"
                            return true;
                            //throw new HcsException(
                            //    $"Failed validation of PPAK server's HTTPS certificate:" +
                            //    $" SerialNumber={serverCertificate.GetSerialNumberString()}" +
                            //    $" EffectiveDate=[{serverCertificate.GetEffectiveDateString()}]" +
                            //    $" Subject=[{serverCertificate.Subject}] Issurer=[{serverCertificate.Issuer}]");
                        };
                }
            }

            if (!_config.UseTunnel) {
                clientCredentials.ClientCertificate.SetCertificate(
                 StoreLocation.CurrentUser,
                 StoreName.My,
                 X509FindType.FindByThumbprint,
                 _config.CertificateThumbprint);
            }
        }

        /// <summary>
        /// Выполнение одной попытки пооучить результат операции. 
        /// Реализуется в производных классах. 
        /// </summary>
        protected abstract Task<IHcsGetStateResult> TryGetResultAsync(IHcsAck sourceAck, CancellationToken token);

        /*
         Основной алгоритм ожидания ответа на асинхронный запрос.  
         Из документации ГИС ЖКХ:
         Также рекомендуем придерживаться следующего алгоритма отправки запросов на получение статуса обработки пакета в случае использования асинхронных сервисов ГИС ЖКХ (в рамках одного MessageGUID):
         - первый запрос getState направлять не ранее чем через 10 секунд, после получения квитанции о приеме пакета с бизнес-данными от сервиса ГИС КЖХ;
         - в случае, если на первый запрос getSate получен результат с RequestState равным "1" или "2", то следующий запрос getState необходимо направлять не ранее чем через 60 секунд после отправки предыдущего запроса;
         - в случае, если на второй запрос getSate получен результат с RequestState равным "1" или "2", то следующий запрос getState необходимо направлять не ранее чем через 300 секунд после отправки предыдущего запроса;
         - в случае, если на третий запрос getSate получен результат с RequestState равным "1" или "2", то следующий запрос getState необходимо направлять не ранее чем через 900 секунд после отправки предыдущего запроса;
         - в случае, если на четвертый (и все последующие запросы) getState получен результат с RequestState равным "1" или "2", то следующий запрос getState необходимо направлять не ранее чем через 1800 секунд после отправки предыдущего запроса.* 
        */
        protected async Task<IHcsGetStateResult> WaitForResultAsync(
            IHcsAck ack, bool withInitialDelay, CancellationToken token)
        {
            // бесконечное количество попыток
            var startTime = DateTime.Now;
            IHcsGetStateResult result;
            for (int attempts = 1; ; attempts++) {
                token.ThrowIfCancellationRequested();

                int delaySec = EnableMinimalResponseWaitDelay ? 2 : 5;
                if (attempts >= 2) delaySec = 5;
                if (attempts >= 3) delaySec = 10;
                if (attempts >= 5) delaySec = 20;
                if (attempts >= 7) delaySec = 40;
                if (attempts >= 9) delaySec = 80;
                if (attempts >= 12) delaySec = 300; // остальные по 5 минут

                if (attempts > 1 || withInitialDelay) {

                    var minutesElapsed = (int)(DateTime.Now - startTime).TotalMinutes;
                    if (CanBeRestarted && minutesElapsed > RestartTimeoutMinutes) 
                        throw new HcsRestartTimeoutException($"Превышено ожидание в {RestartTimeoutMinutes} минут");

                    _config.Log($"Ожидаю {delaySec} сек. до попытки #{attempts}" +
                                $" получить ответ (ожидание {minutesElapsed} минут(ы))...");

                    await Task.Delay(delaySec * 1000, token);
                }

                _config.Log($"Запрашиваю ответ, попытка #{attempts} {ThreadIdText}...");
                result = await TryGetResultAsync(ack, token);
                if (result != null) break;
            }

            _config.Log($"Ответ получен, число частей: {result.Items.Count()}");
            return result;
        }

        /// <summary>
        /// Исполнение повторяемой операции некоторое дпустимое число ошибок.
        /// </summary>
        public async Task<T> RunRepeatableTaskAsync<T>(
            Func<Task<T>> taskFunc, Func<Exception, bool> canIgnoreFunc, int maxAttempts)
        {
            for (int attempts = 1; ; attempts++) {
                try {
                    return await taskFunc();
                }
                catch (Exception e) {
                    if (canIgnoreFunc(e)) {
                        if (attempts < maxAttempts) {
                            Log($"Игнорирую {attempts} из {maxAttempts} допустимых ошибок");
                            continue;
                        }
                        throw new HcsException(
                            $"Более {maxAttempts} продолжений после допустимых ошибок", e);
                    }
                    throw new HcsException("Вложенная ошибка", e);
                }
            }
        }

        /// <summary>
        /// Для запросов к серверу которые можно направлять несколько раз, разрешаем
        /// серверу аномально отказаться. Предполагается что здесь мы игнорируем
        /// только жесткие отказы серверной инфраструктуры, которые указывают
        /// что запрос даже не был принят в обработку. Также все запросы на
        /// чтение можно повторять в случае их серверных системных ошибок.
        /// </summary>
        protected async Task<T> RunRepeatableTaskInsistentlyAsync<T>(
            Func<Task<T>> func, CancellationToken token)
        {
            int afterErrorDelaySec = 120;
            for (int attempt = 1; ; attempt++) {
                try {
                    return await func();
                }
                catch (Exception e) {
                    string marker;
                    if (CanIgnoreSuchException(e, out marker)) {
                        _config.Log($"Игнорирую ошибку #{attempt} типа [{marker}].");
                        _config.Log($"Ожидаю {afterErrorDelaySec} сек. до повторения после ошибки...");
                        await Task.Delay(afterErrorDelaySec * 1000, token);
                        continue;
                    }

                    if (e is HcsRestartTimeoutException) 
                        throw new HcsRestartTimeoutException("Наступило событие рестарта", e);

                    // ошибки удаленной системы которые нельзя игнорировать дублируем для точности перехвата
                    if (e is HcsRemoteException) throw HcsRemoteException.CreateNew(e as HcsRemoteException);
                    throw new HcsException("Ошибка, которую нельзя игнорировать", e);
                }
            }
        }

        //"[EXP001000] Произошла ошибка при передаче данных. Попробуйте осуществить передачу данных повторно",
        // Видимо, эту ошибку нельзя включать здесь. Предположительно это маркер DDOS защиты и если отправлять
        // точно-такой же пакет повторно, то ошибка входит в бесконечный цикл - необходимо заново
        // собирать пакет с новыми кодами и временем и новой подписью. Такую ошибку надо обнаруживать
        // на более высоком уровне и заново отправлять запрос новым пакетом. (21.09.2022)

        private static string[] ignorableSystemErrorMarkers = {
            "Истекло время ожидания шлюза",
            "Базовое соединение закрыто: Соединение, которое должно было работать, было разорвано сервером",
            "Попробуйте осуществить передачу данных повторно", // включено 18.10.2024 HouseManagement API сильно сбоит
            "(502) Недопустимый шлюз",
            "(503) Сервер не доступен"
        };

        private bool CanIgnoreSuchException(Exception e, out string resultMarker)
        {
            foreach (var marker in ignorableSystemErrorMarkers) {
                var found = HcsUtil.EnumerateInnerExceptions(e).Find(
                    x => x.Message != null && x.Message.Contains(marker));
                if (found != null) {
                    resultMarker = marker;
                    return true;
                }
            }

            resultMarker = null;
            return false;
        }

        /// <summary>
        /// Проверяет массив @items на содержание строго одного элемента типа @T и этот элемент.
        /// </summary>
        protected T RequireSingleItem<T>(object[] items)
        {
            if (items == null) 
                throw new HcsException($"Array of type {typeof(T)} must not be null");
            if (items.Length == 0)
                throw new HcsException($"Array of type {typeof(T)} must not be empty");
            if (items.Length > 1) 
                throw new HcsException($"Array of type {typeof(T)} must contain 1 element, not {items.Length} of type {items[0].GetType().FullName}");
            return RequireType<T>(items[0]); 
        }

        /// <summary>
        /// Проверяет @obj на соответствие типу @T и возвращает преобразованный объект.
        /// </summary>
        protected T RequireType<T>(object obj)
        {
            if (obj != null) {
                if (typeof(T) == obj.GetType()) return (T)obj;
            }

            throw new HcsException(
                $"Require object of type {typeof(T)} but got" +
                (obj == null ? "null" : obj.GetType().FullName));
        }

        internal static HcsException NewUnexpectedObjectException(object obj)
        {
            if (obj == null) return new HcsException("unexpected object is null");
            return new HcsException($"Unexpected object [{obj}] of type {obj.GetType().FullName}");
        }

        public static string FormatGuid(Guid guid) => HcsUtil.FormatGuid(guid);

        public static string FormatGuid(Guid? guid) => (guid != null) ? FormatGuid((Guid)guid) : null;

        public static Guid ParseGuid(string guid) => HcsUtil.ParseGuid(guid);

        public static Guid ParseGuid(object obj)
        {
            if (obj == null) throw new HcsException("Can't parse null as Guid");
            if (obj is Guid) return (Guid)obj;
            return ParseGuid(obj.ToString());
        }

        public static Guid[] ParseGuidArray(string[] array)
        {
            if (array == null) return null;
            return array.ToList().Select(x => ParseGuid(x)).ToArray();
        }

        public bool IsArrayEmpty(Array a) => (a == null || a.Length == 0);

        public string MakeEmptyNull(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>
        /// Выполняет @action на объекте @x если объект не пустой и приводится к типу @T.
        /// </summary>
        public void CallOnType<T>(object x, Action<T> action) where T : class
        {
            var t = x as T;
            if (t != null) action(t);
        }

        /// <summary>
        /// Возвращает индентификатор текущего исполняемого потока.
        /// </summary>
        public int ThreadId => System.Environment.CurrentManagedThreadId;

        public string ThreadIdText => $"(thread #{ThreadId})";

        public void Log(string message) => ClientConfig.Log(message);
    }
}
