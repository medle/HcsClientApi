using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    public class HcsConsoleLogger: IHcsLogger
    {
        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }
}
