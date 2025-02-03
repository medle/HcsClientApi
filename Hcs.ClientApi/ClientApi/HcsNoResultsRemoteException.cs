using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Исключение указывает на то что сервер обнаружил что у 
    /// него нет объектов для выдачи по условию.
    /// </summary>
    public class HcsNoResultsRemoteException : HcsRemoteException
    {
        public HcsNoResultsRemoteException(string description) : 
            base(HcsRemoteException.KnownCodes.НетОбъектовДляЭкспорта, description)
        {
        }

        public HcsNoResultsRemoteException(string errorCode, string description) : 
            base(errorCode, description)
        { 
        }

        public HcsNoResultsRemoteException(string errorCode, string description, Exception nested) : 
            base(errorCode, description, nested)
        {
        }
    }
}
