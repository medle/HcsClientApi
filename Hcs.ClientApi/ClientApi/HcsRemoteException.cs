
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Сообщение об ошибке возникшей на удаленном сервере ГИС ЖКХ.
    /// </summary>
    public class HcsRemoteException : HcsException
    {
        public string ErrorCode { get; private set; }
        public string Description { get; private set; }

        // известные коды ошибок сервера
        public class KnownCodes 
        {
            public const string НетОбъектовДляЭкспорта = "INT002012";
            public const string ОтсутствуетВРеестре = "INT002000";
            public const string ДоступЗапрещен = "AUT011003";
        }

        public HcsRemoteException(string errorCode, string description) 
            : base(MakeMessage(errorCode, description))
        {
            this.ErrorCode = errorCode;
            this.Description = description;
        }

        public HcsRemoteException(string errorCode, string description, Exception nestedException) 
            : base(MakeMessage(errorCode, description), nestedException)
        {
            this.ErrorCode = errorCode;
            this.Description = description;
        }

        private static string MakeMessage(string errorCode, string description)
            => $"Удаленная система вернула ошибку: [{errorCode}] {description}";

        public static HcsRemoteException CreateNew(string errorCode, string description, Exception nested = null)
        {
            if (string.Compare(errorCode, KnownCodes.НетОбъектовДляЭкспорта) == 0)
                return new HcsNoResultsRemoteException(errorCode, description, nested);
            return new HcsRemoteException(errorCode, description);
        }

        public static HcsRemoteException CreateNew(HcsRemoteException nested)
        {
            if (nested == null) throw new ArgumentNullException("nested exception");
            return CreateNew(nested.ErrorCode, nested.Description, nested);
        }

        /// <summary>
        /// Возвращает true если ошибка @e или ее вложенные ошибки модержат @errorCode.
        /// </summary>
        public static bool ContainsErrorCode(Exception e, string errorCode)
        {
            if (e == null) return false;
            return HcsUtil.EnumerateInnerExceptions(e).OfType<HcsRemoteException>().Where(x => x.ErrorCode == errorCode).Any();
        }
    }
}
