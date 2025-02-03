using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    public class HcsUtil
    {
        /// <summary>
        /// Возвращает описание исключения одной строкой.
        /// </summary>
        public static string DescribeException(Exception e)
        {
            string separator = "";
            var buf = new StringBuilder();
            buf.Append("[");
            foreach (var inner in EnumerateInnerExceptions(e)) {
                buf.Append(separator);
                buf.Append(inner.GetType().Name);
                buf.Append(":");
                buf.Append(inner.Message);
                separator = "-->";
            }

            buf.Append("]");
            return buf.ToString();
        }

        /// <summary>
        /// Возвращает список все вложенных исключений для данного исключения.
        /// </summary>
        public static List<Exception> EnumerateInnerExceptions(Exception e)
        {
            var list = new List<Exception>();
            WalkInnerExceptionsRecurse(e, list);
            return list;
        }

        private static void WalkInnerExceptionsRecurse(Exception e, List<Exception> list)
        {
            if (e == null || list.Contains(e)) return;
            list.Add(e);

            WalkInnerExceptionsRecurse(e.InnerException, list);

            if (e is AggregateException) {
                var aggregate = e as AggregateException;
                foreach (var inner in aggregate.InnerExceptions) {
                    WalkInnerExceptionsRecurse(inner, list);
                }
            }
        }

        public static string FormatGuid(Guid guid) => guid.ToString();

        public static Guid ParseGuid(string guid)
        {
            try {
                return Guid.Parse(guid);
            }
            catch (Exception e) {
                throw new HcsException($"Невозможно прочитать GUID из строки [{guid}]", e);
            }
        }

        public static string FormatDate(DateTime date)
        {
            return date.ToString("yyyyMMdd");
        }

        public static string FormatDate(DateTime? date)
        {
            return (date == null) ? string.Empty : FormatDate((DateTime)date);
        }

        /// <summary>
        /// Преобразует массиб байтов в строку в формате binhex.
        /// </summary>
        public static string ConvertToHexString(byte[] ba)
        {
            var buf = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba) buf.AppendFormat("{0:x2}", b);
            return buf.ToString();
        }
    }
}
