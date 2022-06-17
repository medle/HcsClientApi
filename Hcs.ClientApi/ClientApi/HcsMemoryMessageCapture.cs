
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Реализация захвата содержимого отправляемых и принимаемых SOAP сообщений,
    /// которая хранит данные в памяти.
    /// </summary>
    public class HcsMemoryMessageCapture : IHcsMessageCapture
    {
        private MemoryStream messageCaptureStream;
        private StreamWriter messageCaptureWriter;

        // сообщения SOAP/XML для ГИС ЖКХ форматируются в UTF8
        private Encoding encoding => Encoding.UTF8;

        public HcsMemoryMessageCapture()
        {
            messageCaptureStream = new MemoryStream();
            messageCaptureWriter = new StreamWriter(messageCaptureStream, encoding);
        }

        void IHcsMessageCapture.CaptureMessage(bool sentOrReceived, string messageBody)
        {
            if (messageCaptureStream.Position > 0) messageCaptureWriter.WriteLine("");
            messageCaptureWriter.Write("<!--");
            messageCaptureWriter.Write(sentOrReceived ? "SENT " : "RECV ");
            messageCaptureWriter.Write(DateTime.Now.ToString());
            messageCaptureWriter.WriteLine("-->");
            messageCaptureWriter.Write(messageBody);
            messageCaptureWriter.Flush();
        }

        public byte[] GetData()
        {
            var buf = messageCaptureStream.GetBuffer();
            int size = (int)messageCaptureStream.Length;
            var data = new byte[size];
            Buffer.BlockCopy(buf, 0, data, 0, size);
            return data;
        }

        public override string ToString()
        {
            return encoding.GetString(GetData());
        }
    }
}
