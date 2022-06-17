
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
   /// <summary>
   /// Реализация механизма захвата содержимого сообщений SOAP записывающая
   /// каждое сообщение в отдельный файл на диске.
   /// </summary>
   public class HcsFileWriterMessageCapture : IHcsMessageCapture
   {
        private int index;
        private string directory;
        private IHcsLogger logger;

        public HcsFileWriterMessageCapture()
        { 
        }

        public HcsFileWriterMessageCapture(string directory, IHcsLogger logger)
        {
            this.directory = directory;
            this.logger = logger;
        }

        public void CaptureMessage(bool sent, string body)
        {
            index += 1;
            string fileName = index.ToString("D3") + "_" + (sent ? "message" : "response") + ".xml";
            if (!string.IsNullOrEmpty(directory)) {
                fileName = System.IO.Path.Combine(directory, fileName);
            }

            if(logger != null) logger.WriteLine($"Writing message file: {fileName}...");
            System.IO.File.WriteAllText(fileName, body, Encoding.UTF8);
        }
    }
}
