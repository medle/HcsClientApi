
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Интерфейс для механизма вывода отладочных сообщений для обработки вызывающей системой.
    /// </summary>
    public interface IHcsLogger
    {
        void WriteLine(string message);
    }
}
