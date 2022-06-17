using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Интерфейс для механизма захвата отправляемых и принимаемых 
    /// SOAP сообщений в ходе коммуникации с ГИС ЖКХ.
    /// </summary>
    public interface IHcsMessageCapture
    {
        void CaptureMessage(bool sentOrReceived, string messageBody);
    }
}
