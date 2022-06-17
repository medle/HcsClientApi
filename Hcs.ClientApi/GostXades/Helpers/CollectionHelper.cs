using System.Collections;
using System.Linq;

namespace Hcs.GostXades.Helpers
{
    public static class CollectionHelper
    {
        public static bool IsNotEmpty(this IEnumerable enumerable)
        {
            return enumerable != null && enumerable.OfType<object>().Any();
        }
    }
}