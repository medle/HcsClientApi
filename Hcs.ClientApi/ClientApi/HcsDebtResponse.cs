using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Ответ на запрос о наличии задолженности.
    /// </summary>
    public class HcsDebtResponse
    {
        public Guid TransportGuid;  // идентификатор ответа в отправляющей системе
        public Guid SubrequestGuid; // идентификатор подзапроса
        public bool HasDebt;
        public HcsPersonalData[] PersonalData;
        public string Description;
    }

    /// <summary>
    /// Сведения о должнике.
    /// </summary>
    public class HcsPersonalData
    {
        public string FirstName;
        public string MiddleName;
        public string LastName;
    }

    /// <summary>
    /// Результат отправки ответа на запрос о наличии задолженности.
    /// </summary>
    public class HcsDebtResponseResult
    {
        public Guid TransportGuid;   // идентификатор ответа в отправляющей системе
        public Guid SubrequestGuid;  // идентификатор подзапроса
        public Exception Error;      // ожибка отправки если указано
        public DateTime UpdateDate;  // дата успешного приема ответа если не указана ошибка
        public bool HasError => (Error != null);
    }
}
