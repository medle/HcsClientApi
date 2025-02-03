using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.DataTypes
{
    public class ГисПоказания
    {
        public DateTime ДатаСнятия;
        public string ПоказанияТ1;
        public string ПоказанияТ2;
        public string ПоказанияТ3;

        public override string ToString()
        {
            var buf = new StringBuilder();

            if (!string.IsNullOrEmpty(ПоказанияТ1)) {
                buf.AppendFormat("Т1={0}", ПоказанияТ1);
            }

            if (!string.IsNullOrEmpty(ПоказанияТ2)) {
                if (buf.Length > 0) buf.Append(" ");
                buf.AppendFormat("Т2={0}", ПоказанияТ2);
            }

            if (!string.IsNullOrEmpty(ПоказанияТ3)) {
                if (buf.Length > 0) buf.Append(" ");
                buf.AppendFormat("Т3={0}", ПоказанияТ3);
            }

            if (ДатаСнятия != default) {
                if (buf.Length > 0) buf.Append(" ");
                buf.AppendFormat("на {0:d}", ДатаСнятия);
            }

            return buf.ToString();
        }
    }
}
