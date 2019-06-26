using System.Collections.Generic;
using System.Linq;
using SquidDraftLeague.Settings;

namespace SquidDraftLeague.Draft.Extensions
{
    public static class LinqExtensions
    {
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> sequence)
        {
            T[] retArray = sequence.ToArray();

            for (int i = 0; i < retArray.Length - 1; i += 1)
            {
                int swapIndex = Globals.Random.Next(i, retArray.Length);
                if (swapIndex == i) continue;
                T temp = retArray[i];
                retArray[i] = retArray[swapIndex];
                retArray[swapIndex] = temp;
            }

            return retArray;
        }
    }
}
