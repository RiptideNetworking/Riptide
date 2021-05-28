using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking
{
    /// <summary>Provides functionality for logging messages.</summary>
    public class RiptideLogger
    {
        /// <summary>Encapsulates a method used to log messages.</summary>
        /// <param name="log">The message to log.</param>
        public delegate void LogMethod(string log);
        private static LogMethod logMethod;
        private static bool includeTimestamps;
        private static string timestampFormat;

        /// <summary>Initializes the logger.</summary>
        /// <param name="logMethod">The method to use when logging messages.</param>
        /// <param name="includeTimestamps">Whether or not to include timestamps when logging messages.</param>
        /// <param name="timestampFormat">The format to use for timestamps.</param>
        public static void Initialize(LogMethod logMethod, bool includeTimestamps, string timestampFormat = "HH:mm:ss")
        {
            RiptideLogger.logMethod = logMethod;
            RiptideLogger.includeTimestamps = includeTimestamps;
            RiptideLogger.timestampFormat = timestampFormat;
        }

        /// <summary>Logs a message.</summary>
        /// <param name="message">The message to log to the console.</param>
        public static void Log(string message)
        {
            if (includeTimestamps)
                logMethod($"[{GetTimestamp(DateTime.Now)}]: {message}");
            else
                logMethod(message);
        }
        /// <summary>Logs a message.</summary>
        /// <param name="logName">Who is logging this message.</param>
        /// <param name="message">The message to log to the console.</param>
        public static void Log(string logName, string message)
        {
            if (includeTimestamps)
                logMethod($"[{GetTimestamp(DateTime.Now)}] ({logName}): {message}");
            else
                logMethod($"({logName}): {message}");
        }

        /// <summary>Converts a DateTime object to a formatted timestamp string.</summary>
        /// <param name="time">The time to format.</param>
        /// <returns>The formatted timestamp.</returns>
        private static string GetTimestamp(DateTime time)
        {
#if DETAILED_LOGGING
            return time.ToString("HH:mm:ss:fff");
#else
            return time.ToString(timestampFormat);
#endif
        }
    }
}
