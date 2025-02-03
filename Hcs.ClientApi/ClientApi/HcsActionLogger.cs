using Hcs.ClientApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    public class HcsActionLogger : IHcsLogger
    {
        private Action<string> logger;

        public HcsActionLogger(Action<string> logger)
        {
            this.logger = logger;
        }

        public void WriteLine(string message)
        {
            logger(message);
        }
    }
}

