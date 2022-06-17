using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GostCryptography.Config;

namespace Hcs.GostXades.Helpers
{
    public static class GostHashAlgorithmHelper
    {
        /// <summary>
        /// Расчитать HASH
        /// </summary>
        /// <param name="cryptoProviderType">Тип Критопровайдера</param>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ComputeHash(CryptoProviderTypeEnum cryptoProviderType, byte[] bytes)
        {
            byte[] hashValue;
            GostCryptoConfig.ProviderType = (GostCryptography.Base.ProviderType)cryptoProviderType;
            var hashAlgorithm = new GostCryptography.Gost_R3411.Gost_R3411_2012_512_HashAlgorithm();
            hashValue = hashAlgorithm.ComputeHash(bytes);
            return BitConverter.ToString(hashValue).Replace("-", "");
        }
    }
}
