using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using SixLabors.Fonts;

namespace SquidDraftLeague.Bot
{
    public static class Globals
    {
        public static List<ulong> SuperUsers => GetSuperUsers();

        private static List<ulong> GetSuperUsers()
        {
            if (!File.Exists(Path.Combine(AppPath, "Data", "superusers.json")))
            {
                File.WriteAllText(Path.Combine(AppPath, "Data", "superusers.json"), JsonConvert.SerializeObject(new List<ulong>(), Formatting.Indented));
            }

            return JsonConvert.DeserializeObject<List<ulong>>(File.ReadAllText(Path.Combine(AppPath, "Data", "superusers.json")));
        }

        /// <summary>
        /// Gets or sets the bots settings.
        /// </summary>
        public static Settings BotSettings { get; set; }

        /// <summary>
        /// Returns the root directory of the application.
        /// </summary>
        public static readonly string AppPath = Directory.GetParent(new Uri(Assembly.GetEntryAssembly()?.CodeBase).LocalPath).FullName;

        public static readonly FontCollection Fonts = new FontCollection();
        public static readonly FontFamily KarlaFontFamily = Fonts.Install(Path.Combine(AppPath, "Data", "font", "Karla-Regular.ttf"));
        public static readonly FontFamily KarlaBoldFontFamily = Fonts.Install(Path.Combine(AppPath, "Data", "font", "Karla-Bold.ttf"));
        public static readonly FontFamily KarlaBoldItalicFontFamily = Fonts.Install(Path.Combine(AppPath, "Data", "font", "Karla-BoldItalic.ttf"));
        public static readonly FontFamily KarlaItalicFontFamily = Fonts.Install(Path.Combine(AppPath, "Data", "font", "Karla-Italic.ttf"));

        /// <summary>
        /// My implementation of a static random; as close to fully random as possible.
        /// </summary>
        public static Random Random => local ?? (local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));

        [ThreadStatic] private static Random local;
    }
}
