using System;
using Hcs.GostXades.Abstractions;
using Hcs.GostXades.Helpers;

namespace Hcs.GostXades
{
    public class GostXadesBesService : IXadesService
    {
        CryptoProviderTypeEnum cryptoProviderType;
        public GostXadesBesService(CryptoProviderTypeEnum cryptoProviderType)
        {
            this.cryptoProviderType = cryptoProviderType;
        }

        public void ValidateSignature(string xmlData, string elementId)
        {
            if (string.IsNullOrEmpty(xmlData))
            {
                throw new ArgumentNullException(nameof(xmlData));
            }
            if (string.IsNullOrWhiteSpace(elementId))
            {
                throw new ArgumentNullException(nameof(elementId));
            }

            var document = XmlDocumentHelper.Create(xmlData);
            var signedXml = new XadesBesSignedXml(document, elementId)
            {
                CertificateMatcher = new CertificateMatcher(new GostCryptoProvider(this.cryptoProviderType))
            };
            signedXml.Validate();
        }

        public string Sign(string xmlData, string elementId, string certificateThumbprint, string certificatePassword)
        {
            if (string.IsNullOrEmpty(xmlData))
            {
                throw new ArgumentNullException(nameof(xmlData));
            }
            if (string.IsNullOrEmpty(elementId))
            {
                throw new ArgumentNullException(nameof(elementId));
            }
            if (string.IsNullOrEmpty(certificateThumbprint))
            {
                throw new ArgumentNullException(nameof(certificateThumbprint));
            }

            var originalDoc = XmlDocumentHelper.Create(xmlData);
            var certificate = CertificateHelper.GetCertificateByThumbprint(certificateThumbprint);

            var provider = new GostCryptoProvider(this.cryptoProviderType);
            var xadesSignedXml = new XadesBesSignedXml(originalDoc)
            {
                SignedElementId = elementId,
                CryptoProvider = provider
            };

            var element = xadesSignedXml.FindElement(elementId, originalDoc);
            if (element == null)
            {
                throw new InvalidOperationException($"�� ������� ����� ���� c Id {elementId}");
            }

            xadesSignedXml.ComputeSignature(certificate, certificatePassword);
            xadesSignedXml.InjectSignatureTo(originalDoc);

            return originalDoc.OuterXml;
        }
    }
}