using System;
using System.Collections.Generic;
using System.Text;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class IntegerExtensions
    {
        public static string GetOrdinal(this int num)
        {
            if (num <= 0) return num.ToString();

            return (num % 100) switch
            {
                11 => "th",
                12 => "th",
                13 => "th",
                _ => ((num % 10) switch
                {
                    1 => "st",
                    2 => "nd",
                    3 => "rd",
                    _ => "th"
                })
            };
        }
    }
}
