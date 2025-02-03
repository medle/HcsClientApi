
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Asn1;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Методы работы с сертификатами X509, которых нет в системе.
    /// </summary>
    public class HcsX509Tools
    {
        public static bool IsValidCertificate(X509Certificate2 cert)
        {
            var now = DateTime.Now;
            return (now >= cert.NotBefore && now <= GetNotAfterDate(cert));
        }

        public static IEnumerable<X509Certificate2> EnumerateCertificates(bool includeInvalid = false)
        {
            // получаем доступ к хранилищу сертификатов
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            try {
                var now = DateTime.Now;
                foreach (var x in store.Certificates) {
                    if (includeInvalid) yield return x;
                    else if (IsValidCertificate(x)) yield return x;
                }
            }
            finally {
                store.Close();
            }
        }

        public static X509Certificate2 FindCertificate(Func<X509Certificate2, bool> predicate)
        {
            if (predicate == null) throw new ArgumentException("Null subject predicate");
            return EnumerateCertificates(true).FirstOrDefault(x => predicate(x));
        }

        public static X509Certificate2 FindValidCertificate(Func<X509Certificate2, bool> predicate)
        {
            if (predicate == null) throw new ArgumentException("Null subject predicate");
            return EnumerateCertificates(false).FirstOrDefault(x => predicate(x));
        }

        /// <summary>
        /// Возвращает Common Name сертификата.
        /// </summary>
        public static string GetCommonName(X509Certificate2 x509cert)
        {
            return x509cert.GetNameInfo(X509NameType.SimpleName, false);
        }

        /// <summary>
        /// Возвращает дату окончания действия сертификата.
        /// </summary>
        public static DateTime GetNotAfterDate(X509Certificate2 x509cert)
        {
            // сначала пытаемся определить срок первичного ключа, а затем уже самого сертификата
            DateTime? датаОкончания = GetPrivateKeyUsageEndDate(x509cert);
            if (датаОкончания != null) return (DateTime)датаОкончания;
            return x509cert.NotAfter;
        }

        /// <summary>
        /// Известные номера расширений сертификата.
        /// </summary>
        private class KnownOids
        {
            public const string PrivateKeyUsagePeriod = "2.5.29.16";
        }

        public static DateTime? GetPrivateKeyUsageEndDate(X509Certificate2 x509cert)
        {
            foreach (var ext in x509cert.Extensions) {
                if (ext.Oid.Value == KnownOids.PrivateKeyUsagePeriod) {
                    // дата начала с индексом 0, дата окончания с индексом 1
                    return ParseAsn1Datetime(ext, 1);
                }
            }

            return null;
        }

        /// <summary>
        /// Разбирает значение типа дата из серии значений ASN1 присоединенных к расширению.
        /// </summary>
        private static DateTime? ParseAsn1Datetime(X509Extension ext, int valueIndex)
        {
            try {
                Asn1Object asnObject = (new Asn1InputStream(ext.RawData)).ReadObject();
                if (asnObject == null) return null;
                var asnSequence = Asn1Sequence.GetInstance(asnObject);
                if (asnSequence.Count <= valueIndex) return null;
                var asn = (Asn1TaggedObject)asnSequence[valueIndex];

                var asnStr = Asn1OctetString.GetInstance(asn, false);
                string s = Encoding.UTF8.GetString(asnStr.GetOctets());
                int year = int.Parse(s.Substring(0, 4));
                int month = int.Parse(s.Substring(4, 2));
                int day = int.Parse(s.Substring(6, 2));
                int hour = int.Parse(s.Substring(8, 2));
                int minute = int.Parse(s.Substring(10, 2));
                int second = int.Parse(s.Substring(12, 2));
                // последний символ - буква 'Z'
                return new DateTime(year, month, day, hour, minute, second);
            }
            catch (Exception) {
                return null;
            }
        }

        public static string ДатьСтрокуФИОСертификатаСДатойОкончания(X509Certificate2 x509cert)
        {
            var фио = ДатьФИОСертификата(x509cert);
            return фио.Фамилия + " " + фио.Имя + " " + фио.Отчество + 
                " до " + GetNotAfterDate(x509cert).ToString("dd.MM.yyyy");
        }

        public static string ДатьСтрокуФИОСертификата(X509Certificate2 x509cert)
        {
            var фио = ДатьФИОСертификата(x509cert);
            return фио.Фамилия + " " + фио.Имя + " " + фио.Отчество;
        }

        /// <summary>
        /// Возвращает массив из трех строк, содержащих соответственно Фамилию, Имя и Отчество
        /// полученных из данных сертификата. Если сертификат не содержит ФИО возвращается массив
        /// из трех пустых строк. Это не точный метод определять имя, он предполагает что
        /// поля SN,G,CN содержат ФИО в определенном порядке, что правдоподобно но не обязательно.
        /// </summary>
        public static (string Фамилия, string Имя, string Отчество) ДатьФИОСертификата(X509Certificate2 x509cert)
        {
            string фам = "", имя = "", отч = "";

            // сначала ищем поля surname (SN) и given-name (G)
            string sn = DecodeSubjectField(x509cert, "SN");
            string g = DecodeSubjectField(x509cert, "G");
            if (!string.IsNullOrEmpty(sn) && !string.IsNullOrEmpty(g)) {
                фам = sn;
                string[] gParts = g.Split(' ');
                if (gParts != null && gParts.Length >= 1) имя = gParts[0];
                if (gParts != null && gParts.Length >= 2) отч = gParts[1];
            }
            else {

                // иначе берем три первых слова из common name (CN), игнорируя кавычки
                string cn = DecodeSubjectField(x509cert, "CN");
                if (!string.IsNullOrEmpty(cn)) {
                    cn = new StringBuilder(cn).Replace("\"", "").ToString();
                    char[] separators = { ' ', ';' };
                    string[] cnParts = cn.Split(separators);
                    if (cnParts != null && cnParts.Length >= 1) фам = cnParts[0];
                    if (cnParts != null && cnParts.Length >= 2) имя = cnParts[1];
                    if (cnParts != null && cnParts.Length >= 3) отч = cnParts[2];
                }
            }

            return (фам, имя, отч);
        }

        /// <summary>
        /// Возвращает строку ИНН владельца сертификата.
        /// </summary>
        public static string ДатьИННСертификата(X509Certificate2 x509cert)
        {
            return DecodeSubjectField(x509cert, "ИНН");
        }

        /// <summary>
        /// Возвращает значение поля с именем @subName включенного в различимое имя Subject.
        /// </summary>
        private static string DecodeSubjectField(X509Certificate2 x509cert, string subName)
        {
            // чтобы посмотреть все поля сертификата
            //System.Diagnostics.Trace.WriteLine("x509decode=" + x509cert.SubjectName.Decode(
            //  X500DistinguishedNameFlags.UseNewLines));

            // декодируем различимое имя на отдельные строки через переводы строк для надежности разбора
            string decoded = x509cert.SubjectName.Decode(X500DistinguishedNameFlags.UseNewLines);
            char[] separators = { '\n', '\r' };
            string[] parts = decoded.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (parts == null) return null;

            // каждая часть начинается с имени и отделяется от значения символом равно
            foreach (string part in parts) {
                if (part.Length <= subName.Length + 1) continue;
                if (part.StartsWith(subName) && part[subName.Length] == '=') {
                    return part.Substring(subName.Length + 1);
                }
            }

            return null;
        }

        public static int Compare(X509Certificate2 x, X509Certificate2 y)
        {
            if (x == null && y != null) return -1;
            if (x != null && y == null) return 1;
            if (x == null && y == null) return 0;

            // сначала сравниваем ФИО
            int sign = string.Compare(ДатьСтрокуФИОСертификата(x), ДатьСтрокуФИОСертификата(y), true);
            if (sign != 0) return sign;

            // затем дату окончания действия
            return GetNotAfterDate(x).CompareTo(GetNotAfterDate(y));
        }

        public class CertificateComparer: IComparer<X509Certificate2>
        {
            public int Compare(X509Certificate2 x, X509Certificate2 y) => HcsX509Tools.Compare(x, y);
        }
    }
}
