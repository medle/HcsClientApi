﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.DebtRequestsApi
{
    /// <summary>
    /// Подзапрос о наличии задолженности за ЖКУ у организаци предоставляющей ЖКУ.
    /// В терминологии ГИСЖКХ это называется Subrequests, потому что сама ГИСЖКХ выбирает организации, 
    /// которым (пере)направляется оригинальный запрос о наличии задолженности направленный его источником 
    /// в ГИСЖКХ.
    /// </summary>
    public class HcsDebtSubrequest
    {
        public enum ResponseStatusType { Sent, NotSent, AutoGenerated }

        public Guid SubrequestGuid;       // идентификатор подзапроса направленный конкретному поставщику ЖКУ 
        public Guid RequestGuid;          // идентификатор первичного запроса направленного соццентром всем поставщикам
        public string RequestNumber;      // номер запроса
        public DateTime SentDate;         // дата направления
        public string Address;            // строка адреса из запроса
        public Guid FiasHouseGuid;        // идентификатор здания в ФИАС
        public Guid GisHouseGuid;         // идентификатор здания в ГИСЖКХ 
        public Guid HМObjectGuid;         // идентификатор помещения в ГИСЖКХ (v14)
        public string HMObjectType;       // тип помещения (v14) 
        public string AddressDetails;     // номер помещения (не заполняется в v14)
        public DateTime DebtStartDate;    // начало периода задолженности
        public DateTime DebtEndDate;      // конец периода задолженности  
        public ResponseStatusType ResponseStatus; // признак отправления запроса  
        public DateTime ResponseDate;     // дата ответа

        public override string ToString()
        {
            return 
                $"ПодзапросОНЗ #{RequestNumber}" +
                $" Address=[{Address}] Details=[{AddressDetails}]" +
                $" HMO={HМObjectGuid} Sent={SentDate} ResponseStatus={ResponseStatus}";
        }
    }
}
