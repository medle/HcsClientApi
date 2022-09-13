using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.Config
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
            foreach (var inner in ListInnerExceptions(e)) {
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
        public static List<Exception> ListInnerExceptions(Exception e)
        {
            var list = new List<Exception>();
            ListInnerExceptionsRecurse(e, list);
            return list;
        }

        private static void ListInnerExceptionsRecurse(Exception e, List<Exception> list)
        {
            if (e == null || list.Contains(e)) return;
            list.Add(e);

            ListInnerExceptionsRecurse(e.InnerException, list);

            if (e is AggregateException) {
                var aggregate = e as AggregateException;
                foreach (var inner in aggregate.InnerExceptions) {
                    ListInnerExceptionsRecurse(inner, list);
                }
            }
        }
    }
}
