
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Hcs.ClientApi.Config;
using Hcs.ClientApi.Interfaces;

namespace Hcs.ClientApi
{
    public abstract class HcsWorkerBase
    {
        public HcsClientConfig ClientConfig { get; private set; }

        public HcsWorkerBase(HcsClientConfig config)
        {
            this.ClientConfig = config;
        }

        public void Log(string message) => ClientConfig.Log(message);

        public Guid ParseGuid(string guid)
        {
            try {
                return Guid.Parse(guid);
            }
            catch (Exception e) {
                throw new HcsException($"Невозможно прочитать GUID из строки [{guid}]", e);
            }
        }
    }
}
