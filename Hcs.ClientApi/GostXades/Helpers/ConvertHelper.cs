using System.Numerics;

namespace Hcs.GostXades.Helpers
{
    public static class ConvertHelper
    {
        public static string BigIntegerToHex(string str)
        {
            return BigInteger.Parse(str).ToString("X");
        }
    }
}