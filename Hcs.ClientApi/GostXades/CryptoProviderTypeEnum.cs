using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.GostXades
{
    /// <summary>
    /// Типы криптопровайдеров
    /// </summary>
    public enum CryptoProviderTypeEnum
    {
        CryptoPRO = GostCryptography.Base.ProviderType.CryptoPro,
        CryptoPro_2012_512 = GostCryptography.Base.ProviderType.CryptoPro_2012_512,
        CryptoPro_2012_1024 = GostCryptography.Base.ProviderType.CryptoPro_2012_1024,
        VipNET = GostCryptography.Base.ProviderType.VipNet
    }
}
