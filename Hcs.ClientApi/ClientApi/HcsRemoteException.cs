
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    public class HcsRemoteException : HcsException
    {
        public string ErrorCode { get; private set; }
        public string Description { get; private set; }

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
    }
}
