using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.Interfaces
{
    public interface IHcsHeaderType
    {
        string MessageGUID { get; set; }
        DateTime Date { get; set; }
    }
}
