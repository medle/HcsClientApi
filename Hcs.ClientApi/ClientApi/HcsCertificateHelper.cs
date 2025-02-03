
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using GostCryptography.Base;

namespace Hcs.ClientApi
{
    public static class HcsCertificateHelper
    {
        public static bool IsGostPrivateKey(this X509Certificate2 certificate)
        {
            try
            {
                if (certificate.HasPrivateKey)
                {
                    var cspInfo = certificate.GetPrivateKeyInfo();
                    if (cspInfo.ProviderType == (int)ProviderType.CryptoPro || 
                        cspInfo.ProviderType == (int)ProviderType.VipNet || 
                        cspInfo.ProviderType == (int)ProviderType.CryptoPro_2012_512 ||
                        cspInfo.ProviderType == (int)ProviderType.CryptoPro_2012_1024)
                        return true;
                    else
                        return false;

                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static Hcs.GostXades.CryptoProviderTypeEnum GetProviderType(this X509Certificate2 certificate)
        {
            return (Hcs.GostXades.CryptoProviderTypeEnum)GetProviderInfo(certificate).Item1;
        }

        public static Tuple<int, string> GetProviderInfo(this X509Certificate2 certificate) {
            if (certificate.HasPrivateKey)
            {
                var cspInfo = certificate.GetPrivateKeyInfo();
                return new Tuple<int, string>(cspInfo.ProviderType, cspInfo.ProviderName);
            }
            else
                throw new Exception("Certificate has no private key");
        }

        public static X509Certificate2 FindCertificate(Func<X509Certificate2, bool> predicate)
        {
            if (predicate == null) throw new ArgumentException("Null subject predicate");
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

            try {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                var collection = store.Certificates
                    .OfType<X509Certificate2>()
                    .Where(x => x.HasPrivateKey && x.IsGostPrivateKey());

                var now = DateTime.Now;
                return collection.First(
                    x => now >= x.NotBefore && now <= x.NotAfter && predicate(x));
            }
            finally {
                store.Close();
            }
        }

        public static X509Certificate2 ShowCertificateUI()
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            try {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                var collection = store.Certificates
                    .OfType<X509Certificate2>()
                    .Where(x => x.HasPrivateKey && x.IsGostPrivateKey());

                string prompt = "Выберите сертификат";
                var cert = X509Certificate2UI.SelectFromCollection(
                    new X509Certificate2Collection(
                        collection.ToArray()), prompt, "", X509SelectionFlag.SingleSelection)[0];
                return cert;
            }
            finally {
                store.Close();
            }
        }
    }
}
