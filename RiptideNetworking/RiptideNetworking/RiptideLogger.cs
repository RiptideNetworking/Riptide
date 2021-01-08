using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking
{
    public class RiptideLogger
    {
        public delegate void LogMethod(string log);
        private static LogMethod logMethod;
        private static bool includeTimestamps;
        private static string timestampFormat;

        public static void Initialize(LogMethod logMethod, bool includeTimestamps, string timestampFormat = "HH:mm:ss")
        {
            RiptideLogger.logMethod = logMethod;
            RiptideLogger.includeTimestamps = includeTimestamps;
            RiptideLogger.timestampFormat = timestampFormat;
        }

        /// <summary>Log a message to the console.</summary>
        /// <param name="message">The message to log to the console.</param>
        public static void Log(string message)
        {
            if (includeTimestamps)
                logMethod($"[{GetTimestamp(DateTime.Now)}]: {message}");
            else
                logMethod(message);
        }
        /// <summary>Log a message to the console.</summary>
        /// <param name="logName">Who is logging this message.</param>
        /// <param name="message">The message to log to the console.</param>
        public static void Log(string logName, string message)
        {
            if (includeTimestamps)
                logMethod($"[{GetTimestamp(DateTime.Now)}] ({logName}): {message}");
            else
                logMethod($"({logName}): {message}");
        }

        private static string GetTimestamp(DateTime time)
        {
            return time.ToString(timestampFormat);
        }
    }
}
