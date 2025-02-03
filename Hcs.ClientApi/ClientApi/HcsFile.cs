
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Hcs.ClientApi
{
    public class HcsFile
    {
        public string FileName { get; private set; }
        public string ContentType { get; private set; }
        public Stream Stream { get; private set; }

        public long Length => Stream.Length;

        public HcsFile(string fileName, string contentType, Stream stream)
        {
            FileName = fileName;
            ContentType = contentType ?? throw new ArgumentNullException(nameof(ContentType));
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        /// <summary>
        /// По имени файла возвращает строку MIME Content-Type или null если тип MIME не найден.
        /// </summary>
        public static string GetMimeContentTypeForFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string extension = Path.GetExtension(fileName).ToLower();
            var mimeType = AllowedMimeTypes.FirstOrDefault(x => x.Extension == extension);
            if (mimeType == null) return null;
            return mimeType.ContentType;
        }

        public record struct MimeType(string Extension, string ContentType);

        /// <summary>
        /// Типы MIME допустимые для загрузки в ГИС ЖКХ.
        /// </summary>
        public static MimeType[] AllowedMimeTypes = 
        {
           new MimeType(".pdf", "application/pdf"),
           new MimeType(".xls", "application/excel"),
           new MimeType(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
           new MimeType(".doc", "application/msword"),
           new MimeType(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
           new MimeType(".rtf", "application/rtf"),
           new MimeType(".jpg", "image/jpeg"),
           new MimeType(".jpeg", "image/jpeg"),
           new MimeType(".tif", "image/tiff"),
           new MimeType(".tiff", "image/tiff")
           // в спецификации есть другие типы файлов .zip, .sgn и т.д.
        };
    }
}
