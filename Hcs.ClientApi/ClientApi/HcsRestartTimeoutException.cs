using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    public class HcsRestartTimeoutException: HcsException
    {
        public HcsRestartTimeoutException(string message) : base(message)
        {
        }

        public HcsRestartTimeoutException(string message, Exception inner): base(message, inner)
        { 
        }
    }
}
