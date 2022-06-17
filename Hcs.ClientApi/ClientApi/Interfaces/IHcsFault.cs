using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.Interfaces
{
    public interface IHcsFault
    {
        string ErrorCode { get; set; }
        string ErrorMessage { get; set; }
    }
}
