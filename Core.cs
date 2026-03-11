using ExileCore;
using Graphics = ExileCore.Graphics;
using System;
using System.IO;

namespace AutoPOE
{
    public static class Core
    {
        public static bool IsBotRunning = false;
        private static DateTime _nextAction = DateTime.Now;
        public static GameController GameController { get; private set; }
        public static Settings Settings { get; private set; }
        public static Graphics Graphics { get; private set; }
        public static Main Plugin { get; private set; }

        /// <summary>
        /// Initializes the core components. This must be called once when the plugin starts.
        /// </summary>
        public static void Initialize(GameController controller, Settings settings, Graphics graphics, Main plugin)
        {
            GameController = controller;
            Settings = settings;
            Graphics = graphics;
            Plugin = plugin;
        }



        public static bool CanUseAction => DateTime.Now > _nextAction;
        public static void ActionPerformed()
        {
            _nextAction = DateTime.Now.AddMilliseconds(Settings.ActionFrequency);
        }

        public static void LogError(string context, Exception ex)
        {
            try
            {
                var pluginDir = AppContext.BaseDirectory;
                var errorPath = Path.Combine(pluginDir, "Errors.txt");
                var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex}\n";
                File.AppendAllText(errorPath, message);
            }
            catch
            {
                // Never throw from logger.
            }
        }

    }
}
