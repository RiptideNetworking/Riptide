// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;

namespace Riptide.Utils
{
    /// <summary>Defines log message types.</summary>
    public enum LogType
    {
        /// <summary>Logs that are used for investigation during development.</summary>
        Debug,
        /// <summary>Logs that provide general information about application flow.</summary>
        Info,
        /// <summary>Logs that highlight abnormal or unexpected events in the application flow.</summary>
        Warning,
        /// <summary>Logs that highlight problematic events in the application flow which will cause unexpected behavior if not planned for.</summary>
        Error
    }

    /// <summary>Provides functionality for logging messages.</summary>
    public class RiptideLogger
    {
        /// <summary>Whether or not <see cref="LogType.Debug"/> messages will be logged.</summary>
        public static bool IsDebugLoggingEnabled => logMethods.ContainsKey(LogType.Debug);
        /// <summary>Whether or not <see cref="LogType.Info"/> messages will be logged.</summary>
        public static bool IsInfoLoggingEnabled => logMethods.ContainsKey(LogType.Info);
        /// <summary>Whether or not <see cref="LogType.Warning"/> messages will be logged.</summary>
        public static bool IsWarningLoggingEnabled => logMethods.ContainsKey(LogType.Warning);
        /// <summary>Whether or not <see cref="LogType.Error"/> messages will be logged.</summary>
        public static bool IsErrorLoggingEnabled => logMethods.ContainsKey(LogType.Error);
        /// <summary>Whether or not exceptions will be passed to a dedicated log method.</summary>
        public static bool IsExceptionLoggingEnabled => exceptionLogMethod != null;
        /// <summary>Encapsulates a method used to log messages.</summary>
        /// <param name="log">The message to log.</param>
        public delegate void LogMethod(string log);
        /// <summary>Encapsulates a method used to log exceptions.</summary>
        /// <param name="exception">The exception to log.</param>
        /// <remarks>This takes nothing but the exception so that methods which log exceptions natively (such as Unity's
        /// <c>Debug.LogException</c>) can be used directly. Any accompanying message is logged separately, just before it.</remarks>
        public delegate void ExceptionLogMethod(Exception exception);

        /// <summary>Log methods, accessible by their <see cref="LogType"/></summary>
        private static readonly Dictionary<LogType, LogMethod> logMethods = new Dictionary<LogType, LogMethod>(4);
        /// <summary>The method to use when logging exceptions, or <see langword="null"/> to log them as <see cref="LogType.Error"/>.</summary>
        private static ExceptionLogMethod exceptionLogMethod;
        /// <summary>Whether or not to include timestamps when logging messages.</summary>
        private static bool includeTimestamps;
        /// <summary>The format to use for timestamps.</summary>
        private static string timestampFormat;

        /// <summary>Initializes <see cref="RiptideLogger"/> with all log types enabled.</summary>
        /// <param name="logMethod">The method to use when logging all types of messages.</param>
        /// <param name="includeTimestamps">Whether or not to include timestamps when logging messages.</param>
        /// <param name="timestampFormat">The format to use for timestamps.</param>
        public static void Initialize(LogMethod logMethod, bool includeTimestamps, string timestampFormat = "HH:mm:ss") => Initialize(logMethod, logMethod, logMethod, logMethod, null, includeTimestamps, timestampFormat);
        /// <summary>Initializes <see cref="RiptideLogger"/> with the supplied log methods.</summary>
        /// <param name="debugMethod">The method to use when logging debug messages. Set to <see langword="null"/> to disable debug logs.</param>
        /// <param name="infoMethod">The method to use when logging info messages. Set to <see langword="null"/> to disable info logs.</param>
        /// <param name="warningMethod">The method to use when logging warning messages. Set to <see langword="null"/> to disable warning logs.</param>
        /// <param name="errorMethod">The method to use when logging error messages. Set to <see langword="null"/> to disable error logs.</param>
        /// <param name="includeTimestamps">Whether or not to include timestamps when logging messages.</param>
        /// <param name="timestampFormat">The format to use for timestamps.</param>
        public static void Initialize(LogMethod debugMethod, LogMethod infoMethod, LogMethod warningMethod, LogMethod errorMethod, bool includeTimestamps, string timestampFormat = "HH:mm:ss") => Initialize(debugMethod, infoMethod, warningMethod, errorMethod, null, includeTimestamps, timestampFormat);
        /// <summary>Initializes <see cref="RiptideLogger"/> with the supplied log methods.</summary>
        /// <param name="debugMethod">The method to use when logging debug messages. Set to <see langword="null"/> to disable debug logs.</param>
        /// <param name="infoMethod">The method to use when logging info messages. Set to <see langword="null"/> to disable info logs.</param>
        /// <param name="warningMethod">The method to use when logging warning messages. Set to <see langword="null"/> to disable warning logs.</param>
        /// <param name="errorMethod">The method to use when logging error messages. Set to <see langword="null"/> to disable error logs.</param>
        /// <param name="exceptionMethod">The method to use when logging exceptions. Set to <see langword="null"/> to use <paramref name="errorMethod"/> instead.</param>
        /// <param name="includeTimestamps">Whether or not to include timestamps when logging messages.</param>
        /// <param name="timestampFormat">The format to use for timestamps.</param>
        /// <remarks>Supplying an <paramref name="exceptionMethod"/> is worthwhile for loggers which accept exceptions directly (such
        /// as Unity's <c>Debug.LogException</c>), as they can display a stack trace leading back to the code that threw.</remarks>
        public static void Initialize(LogMethod debugMethod, LogMethod infoMethod, LogMethod warningMethod, LogMethod errorMethod, ExceptionLogMethod exceptionMethod, bool includeTimestamps, string timestampFormat = "HH:mm:ss")
        {
            logMethods.Clear();

            if (debugMethod != null)
                logMethods.Add(LogType.Debug, debugMethod);
            if (infoMethod != null)
                logMethods.Add(LogType.Info, infoMethod);
            if (warningMethod != null)
                logMethods.Add(LogType.Warning, warningMethod);
            if (errorMethod != null)
                logMethods.Add(LogType.Error, errorMethod);

            exceptionLogMethod = exceptionMethod;
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
                logMethod(FormatLog(logName, message));
        }
        /// <summary>Logs a message along with the exception which caused it.</summary>
        /// <param name="logName">Who is logging this message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception which caused the message to be logged.</param>
        /// <remarks>Exceptions are always logged as <see cref="LogType.Error"/> messages. If an exception method was supplied when
        /// initializing, the exception is given to it in a log of its own, immediately after the one containing the message.
        /// Otherwise the exception is converted to text and included in the message's log.</remarks>
        public static void Log(string logName, string message, Exception exception)
        {
            logMethods.TryGetValue(LogType.Error, out LogMethod errorMethod);

            if (exceptionLogMethod == null)
            {
                errorMethod?.Invoke(FormatLog(logName, $"{message}\n{exception}"));
                return;
            }

            errorMethod?.Invoke(FormatLog(logName, $"{message} See the following log for the exception itself."));
            exceptionLogMethod(exception);
        }

        /// <summary>Logs an exception which was thrown by a subscriber of the event with the given name.</summary>
        /// <param name="logName">Who is logging this message.</param>
        /// <param name="eventName">The name of the event whose subscriber threw the exception.</param>
        /// <param name="exception">The exception that was thrown.</param>
        /// <remarks>Riptide catches these exceptions instead of letting them propagate, as one escaping would skip the rest of the
        /// handling of whatever triggered the event and leave the peer or connection in an inconsistent state.</remarks>
        internal static void LogEventException(string logName, string eventName, Exception exception)
        {
            Log(logName, $"An exception was thrown by a subscriber of the '{eventName}' event!", exception);
        }

        /// <summary>Adds a log name, and a timestamp if they're enabled, to the given message.</summary>
        /// <param name="logName">Who is logging the message.</param>
        /// <param name="message">The message being logged.</param>
        /// <returns>The formatted message.</returns>
        private static string FormatLog(string logName, string message)
        {
            return includeTimestamps ? $"[{GetTimestamp(DateTime.Now)}] ({logName}): {message}" : $"({logName}): {message}";
        }

        /// <summary>Converts a <see cref="DateTime"/> object to a formatted timestamp string.</summary>
        /// <param name="time">The time to format.</param>
        /// <returns>The formatted timestamp.</returns>
        private static string GetTimestamp(DateTime time)
        {
            return time.ToString(timestampFormat);
        }
    }
}
