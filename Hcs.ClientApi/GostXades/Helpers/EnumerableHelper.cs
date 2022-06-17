using System.Collections.Generic;
using System.Linq;

namespace Hcs.GostXades.Helpers
{
    public static class EnumerableHelper
    {
        public static bool AreSequenceEquals(IEnumerable<string> first, IEnumerable<string> second)
        {
            var sortedFirst = first.OrderBy(x => x).ToArray();
            var sortedSecond = second.OrderBy(x => x).ToArray();
            return ArrayHelper.AreEquals(sortedFirst, sortedSecond);
        }
    }
}