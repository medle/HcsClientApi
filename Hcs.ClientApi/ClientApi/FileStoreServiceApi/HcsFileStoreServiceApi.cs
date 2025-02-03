
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hcs.ClientApi.FileStoreServiceApi
{
    /// <summary>
    /// Описание протокола в файле "ГИС ЖКХ. Альбом ТФФ 14.5.0.1.docx"
    /// 2.5 Описание протокола обмена файлами с внешними системами.
    public class HcsFileStoreServiceApi
    {
        public HcsClientConfig Config { get; private set; }

        /// <summary>
        /// Максимальный размер в байтах части файла, которую разрешено 
        /// загружать на сервер по спецификации протокола.
        /// </summary>
        private const int MAX_PART_LENGTH = 5242880;

        public HcsFileStoreServiceApi(HcsClientConfig config)
        {
            this.Config = config;
        }

        /// <summary>
        /// Путь к сервису хранения файлов ГИС ЖКХ на серевере API.
        /// </summary>
        private const string ExtBusFileStoreServiceRest = "ext-bus-file-store-service/rest";

        /// <summary>
        /// Получение файла ранее загруженного в ГИС ЖКХ.
        /// </summary>
        public async Task<HcsFile> DownloadFile(
            Guid fileGuid, HcsFileStoreContext context, CancellationToken token)
        {
            long length = await GetFileLength(context, fileGuid, token);
            if (length <= MAX_PART_LENGTH) return await DownloadSmallFile(fileGuid, context, token);
            return await DownloadLargeFile(fileGuid, length, context, token);
        }

        /// <summary>
        /// Получение файла по частям (не более 5Мб) по GUID файла.
        /// </summary>
        private async Task<HcsFile> DownloadLargeFile(
            Guid fileGuid, long fileSize, HcsFileStoreContext context, CancellationToken token)
        {
            if (fileSize <= MAX_PART_LENGTH) 
                throw new ArgumentException("Too short file for partial download");

            string requestUri = ComposeFileDownloadUri(fileGuid, context);

            var resultStream = new MemoryStream();
            string resultContentType = null;
            using (var client = BuildHttpClient()) {

                long doneSize = 0;
                while (doneSize < fileSize) {

                    long remainderSize = fileSize - doneSize;
                    long partSize = Math.Min(remainderSize, MAX_PART_LENGTH);

                    long fromPosition = doneSize;
                    long toPosition = fromPosition + partSize - 1;

                    var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                    request.Headers.Range = new RangeHeaderValue(fromPosition, toPosition);

                    var response = await SendRequestAsync(client, request, token);
                    resultContentType = response.Content.Headers.ContentType.ToString();

                    long? responseSize = response.Content.Headers.ContentLength;
                    if (responseSize == null || (long)responseSize != partSize)
                        throw new HcsException($"Получена часть файла длиной {responseSize}, а запрашивалась длина {partSize}");

                    using (var partStream = await response.Content.ReadAsStreamAsync()) {
                        partStream.Position = 0;
                        await partStream.CopyToAsync(resultStream);
                    }

                    doneSize += partSize;
                }

                resultStream.Position = 0;
                return new HcsFile(null, resultContentType, resultStream);
            }
        }

        private string ComposeFileDownloadUri(Guid fileGuid, HcsFileStoreContext context)
        {
            string endpointName = $"{ExtBusFileStoreServiceRest}/{context.GetName()}/{HcsUtil.FormatGuid(fileGuid)}?getfile";
            return Config.ComposeEndpointUri(endpointName);
        }

        /// <summary>
        /// Получение файла одной частью (не более 5Мб) по GUID файла.
        /// </summary>
        private async Task<HcsFile> DownloadSmallFile(
            Guid fileGuid, HcsFileStoreContext context, CancellationToken token)
        {
            string requestUri = ComposeFileDownloadUri(fileGuid, context);
            using (var client = BuildHttpClient()) {

                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                var response = await SendRequestAsync(client, request, token);

                return new HcsFile(
                    null,
                    response.Content.Headers.ContentType.ToString(),
                    await response.Content.ReadAsStreamAsync());
            }
        }

        public async Task<Guid> UploadFile(HcsFile file, HcsFileStoreContext context, CancellationToken token)
        {
            if (file is null) throw new ArgumentNullException(nameof(file));

            if (file.Length <= MAX_PART_LENGTH) {
                return await UploadSmallFile(file, context, token);
            }
            else {
                return await UploadLargeFile(file, context, token);
            } 
        }

        /// <summary>
        /// Отправка большого файла по частям.
        /// </summary>
        private async Task<Guid> UploadLargeFile(HcsFile file, HcsFileStoreContext context, CancellationToken token)
        {
            using var client = BuildHttpClient();

            if (file.Length == 0) throw new ArgumentException("Нельзя передавать файл нулевой длины");
            int numParts = (int)Math.Ceiling((double)file.Length / MAX_PART_LENGTH);

            Config.Log($"Запрашиваю UploadID для большого файла {file.FileName}");
            Guid uploadId = await QueryNewUploadId(client, context, file, numParts, token);
            Config.Log($"Получил UploadID {uploadId} для отправки файла {file.FileName} размером {file.Length} байт");

            // передаем все части
            long partOffset = 0;
            for (int partNumber = 1; partNumber <= numParts; partNumber++) {

                long lengthLeft = file.Length - partOffset;
                int partLength = (int)Math.Min(lengthLeft, MAX_PART_LENGTH);
                var partStream = new HcsPartialStream(file.Stream, partOffset, partLength);

                Config.Log($"Отправляю часть {partNumber}/{numParts} размером {partLength} байт для файла {file.FileName}");
                await UploadFilePart(client, context, uploadId, partNumber, partStream, token);
                partOffset += partLength;
            }

            // завершаем передачу
            Config.Log($"Отправляем признак завершения передачи файла {file.FileName}");
            await CompleteUpload(client, context, uploadId, token);

            Config.Log($"Файл {file.FileName} успешно передан, получен код файла {uploadId}");
            return uploadId;
        }

        /// <summary>
        /// Получение кода для загрузки большого файла из нескольких частей менее 5Мб.
        /// </summary>
        private async Task<Guid> QueryNewUploadId(
                HttpClient client, HcsFileStoreContext context, HcsFile file, int numParts, CancellationToken token)
        {
            string endpointName = $"{ExtBusFileStoreServiceRest}/{context.GetName()}/?upload";
            string requestUri = Config.ComposeEndpointUri(endpointName);

            var content = new StringContent("");
            content.Headers.Add("X-Upload-Filename", CleanUploadFileName(file.FileName));
            content.Headers.Add("X-Upload-Length", file.Length.ToString());
            content.Headers.Add("X-Upload-Part-Count", numParts.ToString());

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Content = content;
            var response = await SendRequestAsync(client, request, token);
            return ParseUploadIdFromReponse(response);
        }

        private async Task UploadFilePart(
            HttpClient client, HcsFileStoreContext context, Guid uploadId, int partNumber, Stream partStream, 
            CancellationToken token)
        {
            string endpointName = $"{ExtBusFileStoreServiceRest}/{context.GetName()}/{HcsUtil.FormatGuid(uploadId)}";
            string requestUri = Config.ComposeEndpointUri(endpointName);

            var content = new StreamContent(partStream);
            content.Headers.Add("X-Upload-Partnumber", partNumber.ToString());
            content.Headers.ContentMD5 = ComputeMD5(partStream);

            var request = new HttpRequestMessage(HttpMethod.Put, requestUri);
            request.Content = content;
            await SendRequestAsync(client, request, token);
        }

        private async Task CompleteUpload(HttpClient client, HcsFileStoreContext context, Guid uploadId, CancellationToken token)
        {
            string endpointName = $"{ExtBusFileStoreServiceRest}/{context.GetName()}/{HcsUtil.FormatGuid(uploadId)}?completed";
            string requestUri = Config.ComposeEndpointUri(endpointName);

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            await SendRequestAsync(client, request, token);
        }

        /// <summary>
        /// Загрузка в ГИС ЖКХ файла до 5Мб размером одной операцией.
        /// </summary>
        private async Task<Guid> UploadSmallFile(HcsFile file, HcsFileStoreContext context, CancellationToken token)
        {
            using var client = BuildHttpClient();

            string endpointName = $"{ExtBusFileStoreServiceRest}/{context.GetName()}";
            string requestUri = Config.ComposeEndpointUri(endpointName);

            Config.Log($"Начинаю upload малого файла [{file.FileName}] типа [{file.ContentType}] длиной {file.Length}");

            if (file.Stream.Length != file.Length) 
                throw new HcsException($"Длина файла {file.Length} не соответствует размеру данных {file.Stream.Length}");
            
            // насильно переводим поток на начало чтобы считать все байты
            file.Stream.Position = 0;    

            var content = new StreamContent(file.Stream);
            content.Headers.Add("X-Upload-Filename", CleanUploadFileName(file.FileName));
            content.Headers.ContentMD5 = ComputeMD5(file.Stream);

            var request = new HttpRequestMessage(HttpMethod.Put, requestUri);
            request.Content = content;
            var response = await SendRequestAsync(client, request, token);
            return ParseUploadIdFromReponse(response);
        }

        /// <summary>
        /// Получение информации о загружаемом или загруженном файле.
        /// </summary>
        public async Task<long> GetFileLength(HcsFileStoreContext context, Guid fileId, CancellationToken token)
        {
            using var client = BuildHttpClient();

            string endpointName = $"{ExtBusFileStoreServiceRest}/{context.GetName()}/{HcsUtil.FormatGuid(fileId)}";
            string requestUri = Config.ComposeEndpointUri(endpointName);

            var request = new HttpRequestMessage(HttpMethod.Head, requestUri);
            var response = await SendRequestAsync(client, request, token);

            long length = 0;
            var lengthString = SearchResponseHeader(response, "X-Upload-Length");
            if (!string.IsNullOrEmpty(lengthString) && long.TryParse(lengthString, out length)) return length;
            throw new HcsException("В ответе сервера не указана длина файла");
        }

        /// <summary>
        /// Возвращает вычисленное значение AttachmentHASH для данных файла @stream.
        /// </summary>
        public string ComputeAttachmentHash(Stream stream)
        {
            var client = Config as HcsClient;
            if (client == null) throw new HcsException("Не доступен объект HcsClient для вычиления AttachmentHASH");

            // В декабре 2024 у меня сломалось вычисление AttachmentHASH для файлов, я стал вычислять
            // явно верным алгоритмом ГОСТ94 для больших файлов уже неверное значение суммы. В январе
            // 2025 путем перебора вариантов я обнаружил что ГИСЖКХ теперь вычисляет AttachmentHASH
            // только по первой части большого файла.
            //int hashSourceMaxLength = MAX_PART_LENGTH;
            //if (stream.Length <= hashSourceMaxLength) return client.ComputeGost94Hash(stream);
            //return client.ComputeGost94Hash(new HcsPartialStream(stream, 0, hashSourceMaxLength));

            // 29.01.2025 СТП ГИС ЖКХ ответила что "проведены работы" и теперь
            // я вижу что они снова вычисляют AttachmantHASH по полному файлу
            return client.ComputeGost94Hash(stream);
        }

        private async Task<HttpResponseMessage> SendRequestAsync(
            HttpClient client, HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            Config.Log($"Отправляю запрос {request.Method} \"{request.RequestUri}\"...");

            var response = await client.SendAsync(request, token);
            if (response.IsSuccessStatusCode) return response;
            throw new HcsException(DescribeResponseError(response));
        }

        private Guid ParseUploadIdFromReponse(HttpResponseMessage response)
        {
            string uploadIdheaderName = "X-Upload-UploadID";
            var uploadId = SearchResponseHeader(response, uploadIdheaderName);
            if (uploadId != null) return HcsUtil.ParseGuid(uploadId);
            throw new HcsException($"В ответе сервера нет заголовка {uploadIdheaderName}");
        }

        private HttpClient BuildHttpClient()
        {
            // требуется применить клиентский сертификат SSL
            var _clientHandler = new HttpClientHandler();
            _clientHandler.ClientCertificates.Add(Config.Certificate);
            _clientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;

            var client = new HttpClient(_clientHandler);
            client.DefaultRequestHeaders.Accept.Clear();

            // указываем в заголовке код поставщика информации
            client.DefaultRequestHeaders.Add("X-Upload-OrgPPAGUID", Config.OrgPPAGUID);
            return client;
        }

        private string DescribeResponseError(HttpResponseMessage response)
        {
            string errorHeader = "X-Upload-Error";
            var message = SearchResponseHeader(response, errorHeader);
            if (message != null) {
                if (knownErrors.ContainsKey(message)) return $"{errorHeader}: {message} ({knownErrors[message]})";
                return $"{errorHeader}: {message}";
            }

            return $"HTTP response status {response.StatusCode}";
        }

        private string SearchResponseHeader(HttpResponseMessage response, string headerName)
        {
            if (response.Headers.Any(x => x.Key == headerName)) {
                var pair = response.Headers.First(x => x.Key == headerName);
                if (pair.Value != null && pair.Value.Any()) {
                    return pair.Value.First();
                }
            }

            return null;
        }

        private static Dictionary<string, string> knownErrors = new Dictionary<string, string>() {
            { "FieldValidationException", "не пройдены проверки на корректность заполнения полей (обязательность, формат и т.п.)" },
            { "FileNotFoundException", "не пройдены проверки на существование файла" },
            { "InvalidStatusException", "не пройдены проверки на корректный статус файла" },
            { "InvalidSizeException", "некорректный запрашиваемый размер файла" },
            { "FileVirusInfectionException", "содержимое файла инфицировано" },
            { "FileVirusNotCheckedException", "проверка на вредоносное содержимое не выполнялась" },
            { "FilePermissionException", "организация и внешняя система не имеют полномочий на скачивание файла" },
            { "DataProviderValidationException", "поставщик данных не найден, заблокирован или неактивен" },
            { "CertificateValidationException", "информационная система не найдена по отпечатку или заблокирована" },
            { "HashConflictException", "не пройдены проверки на соответствие контрольной сумме" },
            { "InvalidPartNumberException", "не пройдены проверки на номер части (номер превышает количество частей, указанных в инициализации)" },
            { "ContextNotFoundException", "неверное имя хранилища файлов" },
            { "ExtensionException", "недопустимое расширение файла" },
            { "DetectionException", "не удалось определить MIME-тип загружаемого файла" },
            { "INT002029", "сервис недоступен: выполняются регламентные работы" }
        };

        private byte[] ComputeMD5(System.IO.Stream stream)
        {
            var position = stream.Position;
            var md5 = System.Security.Cryptography.MD5.Create().ComputeHash(stream);
            stream.Position = position;
            return md5;
        }

        /// <summary>
        /// Готовит имя размещаемого файла для помещения в заголовок HTTP-запроса.
        /// </summary>
        private string CleanUploadFileName(string fileName)
        {
            if (fileName is null) return null;

            string bannedSymbols = "<>?:|*%\\\"";

            var buf = new StringBuilder();
            foreach (char ch in fileName) {
                if (bannedSymbols.Contains(ch)) buf.Append('_');
                else buf.Append(ch);
            }

            // спецификация предписывает кодировать имя файла по стандарту MIME RFC2047
            // https://datatracker.ietf.org/doc/html/rfc2047
            // как имя кодировки можно явно использовать константу "windows-1251"
            string characterSet = Encoding.Default.WebName;
            return EncodedWord.RFC2047.Encode(
                buf.ToString(), EncodedWord.RFC2047.ContentEncoding.Base64, characterSet);
        }
    }
}

