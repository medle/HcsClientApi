
using System;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Xml;
using Hcs.GostXades;
using Hcs.ClientApi.Config;

namespace Hcs.ClientApi.Config
{
    /// <summary>
    /// Фильтр сообщений добавляет в XML сообщение электронную подпись XADES/GOST.
    /// </summary>
    internal class GostSigningMessageInspector : IClientMessageInspector 
    {
        private HcsClientConfig clientConfig;

        public GostSigningMessageInspector(HcsClientConfig clientConfig)
        {
            this.clientConfig = clientConfig;
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel) 
        {
            try
            {
                PurgeDebuggerHeaders(ref request);
                var messageBody = GetMessageBodyString(ref request, Encoding.UTF8);

                if (!messageBody.Contains(HcsConstants.SignElementId)) {
                    clientConfig.MaybeCaptureMessage(true, messageBody);
                }
                else {
                    clientConfig.Log($"Подписываю сообщение ключем {clientConfig.CertificateThumbprint}...");
                    var service = new GostXadesBesService(clientConfig.CryptoProviderType);
                    var signedXml = service.Sign(messageBody,
                        HcsConstants.SignElementId,
                        clientConfig.CertificateThumbprint,
                        clientConfig.CertificatePassword);
                    clientConfig.Log("Сообщение подписано.");

                    clientConfig.MaybeCaptureMessage(true, signedXml);
                    request = Message.CreateMessage(
                        XmlReaderFromString(signedXml), int.MaxValue, request.Version);
                }
            }
            catch (Exception ex)
            {
                string error = $"В {this.GetType().Name} произошло исключение";
                throw new Exception(error,  ex);
            }

            return null;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState) 
        {
            clientConfig.MaybeCaptureMessage(false, reply.ToString());
        }

        private void PurgeDebuggerHeaders(ref Message request) 
        {
            int limit = request.Headers.Count;
            for (int i = 0; i < limit; ++i)
            {
                if (request.Headers[i].Name.Equals("VsDebuggerCausalityData"))
                {
                    request.Headers.RemoveAt(i);
                    break;
                }
            }
        }

        string GetMessageBodyString(ref Message request, Encoding encoding) 
        {
            MessageBuffer mb = request.CreateBufferedCopy(int.MaxValue);

            request = mb.CreateMessage();

            Stream s = new MemoryStream();
            XmlWriter xw = XmlWriter.Create(s);
            mb.CreateMessage().WriteMessage(xw);
            xw.Flush();
            s.Position = 0;

            byte[] bXML = new byte[s.Length];
            s.Read(bXML, 0, (int)s.Length);

            if (bXML[0] != (byte)'<')
            {
                return encoding.GetString(bXML, 3, bXML.Length - 3);
            }
            else
            {
                return encoding.GetString(bXML, 0, bXML.Length);
            }
        }

        XmlReader XmlReaderFromString(String xml) 
        {
            var stream = new MemoryStream();
            var writer = new System.IO.StreamWriter(stream);
            writer.Write(xml);
            writer.Flush();
            stream.Position = 0;
            return XmlReader.Create(stream);
        }
    }
}
