using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Plus0_Bot
{
    public static class Globals
    {
        /// <summary>
        /// Gets or sets the bots settings.
        /// </summary>
        public static Settings BotSettings { get; set; }

        /// <summary>
        /// Returns the root directory of the application.
        /// </summary>
        public static readonly string AppPath = Directory.GetParent(new Uri(Assembly.GetEntryAssembly()?.CodeBase).LocalPath).FullName;

        /// <summary>
        /// My implementation of a static random; as close to fully random as possible.
        /// </summary>
        public static Random Random => local ?? (local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));

        [ThreadStatic] private static Random local;
    }
}
