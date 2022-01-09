
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;

namespace RiptideNetworking.Utils
{
    /// <summary>Defines log message types.</summary>
    public enum LogType
    {
        /// <summary>Logs that are used for investigation during development.</summary>
        debug,
        /// <summary>Logs that provide general information about application flow.</summary>
        info,
        /// <summary>Logs that highlight abnormal or unexpected events in the application flow.</summary>
        warning,
        /// <summary>Logs that highlight problematic events in the application flow which will cause unexpected behavior if not planned for.</summary>
        error
    }

    /// <summary>Provides functionality for logging messages.</summary>
    public class RiptideLogger
    {
        /// <summary>Whether or not <see cref="LogType.debug"/> messages will be logged.</summary>
        public static bool IsDebugLoggingEnabled => logMethods.ContainsKey(LogType.debug);
        /// <summary>Whether or not <see cref="LogType.info"/> messages will be logged.</summary>
        public static bool IsInfoLoggingEnabled => logMethods.ContainsKey(LogType.info);
        /// <summary>Whether or not <see cref="LogType.warning"/> messages will be logged.</summary>
        public static bool IsWarningLoggingEnabled => logMethods.ContainsKey(LogType.warning);
        /// <summary>Whether or not <see cref="LogType.error"/> messages will be logged.</summary>
        public static bool IsErrorLoggingEnabled => logMethods.ContainsKey(LogType.error);
        /// <summary>Encapsulates a method used to log messages.</summary>
        /// <param name="log">The message to log.</param>
        public delegate void LogMethod(string log);

        /// <summary>Log methods, accessible by their <see cref="LogType"/></summary>
        private static readonly Dictionary<LogType, LogMethod> logMethods = new Dictionary<LogType, LogMethod>(4);
        /// <summary>Whether or not to include timestamps when logging messages.</summary>
        private static bool includeTimestamps;
        /// <summary>The format to use for timestamps.</summary>
        private static string timestampFormat;

        /// <summary>Initializes <see cref="RiptideLogger"/> with all log types enabled.</summary>
        /// <param name="logMethod">The method to use when logging all types of messages.</param>
        /// <param name="includeTimestamps">Whether or not to include timestamps when logging messages.</param>
        /// <param name="timestampFormat">The format to use for timestamps.</param>
        public static void Initialize(LogMethod logMethod, bool includeTimestamps, string timestampFormat = "HH:mm:ss") => Initialize(logMethod, logMethod, logMethod, logMethod, includeTimestamps, timestampFormat);
        /// <summary>Initializes <see cref="RiptideLogger"/> with the supplied log methods.</summary>
        /// <param name="debugMethod">The method to use when logging debug messages. Set to <see langword="null"/> to disable debug logs.</param>
        /// <param name="infoMethod">The method to use when logging info messages. Set to <see langword="null"/> to disable info logs.</param>
        /// <param name="warningMethod">The method to use when logging warning messages. Set to <see langword="null"/> to disable warning logs.</param>
        /// <param name="errorMethod">The method to use when logging error messages. Set to <see langword="null"/> to disable error logs.</param>
        /// <param name="includeTimestamps">Whether or not to include timestamps when logging messages.</param>
        /// <param name="timestampFormat">The format to use for timestamps.</param>
        public static void Initialize(LogMethod debugMethod, LogMethod infoMethod, LogMethod warningMethod, LogMethod errorMethod, bool includeTimestamps, string timestampFormat = "HH:mm:ss")
        {
            logMethods.Clear();

            if (debugMethod != null)
                logMethods.Add(LogType.debug, debugMethod);
            if (infoMethod != null)
                logMethods.Add(LogType.info, infoMethod);
            if (warningMethod != null)
                logMethods.Add(LogType.warning, warningMethod);
            if (errorMethod != null)
                logMethods.Add(LogType.error, errorMethod);

            RiptideLogger.includeTimestamps = includeTimestamps;
            RiptideLogger.timestampFormat = timestampFormat;
        }

        /// <summary>Enables logging for messages of the given <see cref="LogType"/>.</summary>
        /// <param name="logType">The type of message to enable logging for.</param>
        /// <param name="logMethod">The method to use when logging this type of message.</param>
        public static void EnableLoggingFor(LogType logType, LogMethod logMethod)
        {
            if (logMethods.ContainsKey(logType))
                logMethods[logType] = logMethod;
            else
                logMethods.Add(logType, logMethod);
        }

        /// <summary>Disables logging for messages of the given <see cref="LogType"/>.</summary>
        /// <param name="logType">The type of message to enable logging for.</param>
        public static void DisableLoggingFor(LogType logType) => logMethods.Remove(logType);

        /// <summary>Logs a message.</summary>
        /// <param name="logType">The type of log message that is being logged.</param>
        /// <param name="message">The message to log.</param>
        public static void Log(LogType logType, string message)
        {
            if (logMethods.TryGetValue(logType, out LogMethod logMethod))
            {
                if (includeTimestamps)
                    logMethod($"[{GetTimestamp(DateTime.Now)}]: {message}");
                else
                    logMethod(message);
            }
        }
        /// <summary>Logs a message.</summary>
        /// <param name="logType">The type of log message that is being logged.</param>
        /// <param name="logName">Who is logging this message.</param>
        /// <param name="message">The message to log.</param>
        public static void Log(LogType logType, string logName, string message)
        {
            if (logMethods.TryGetValue(logType, out LogMethod logMethod))
            {
                if (includeTimestamps)
                    logMethod($"[{GetTimestamp(DateTime.Now)}] ({logName}): {message}");
                else
                    logMethod($"({logName}): {message}");
            }
        }

        /// <summary>Converts a <see cref="DateTime"/> object to a formatted timestamp string.</summary>
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
