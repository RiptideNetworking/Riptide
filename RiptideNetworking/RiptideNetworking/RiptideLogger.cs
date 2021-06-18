using System;

namespace RiptideNetworking
{
    /// <summary>Provides functionality for logging messages.</summary>
    public class RiptideLogger
    {

        #region Variables and Delegates
        /// <summary>The method to use when logging messages.</summary>
        private LogMethod _logMethod;
        /// <summary>Whether or not to include timestamps when logging messages.</summary>
        private bool _includeTimestamps;
        /// <summary>The format to use for timestamps.</summary>
        private string _timestampFormat;

        /// <summary>Encapsulates a method used to log messages.</summary>
        /// <param name="log">The message to log.</param>
        public delegate void LogMethod(string log);
        #endregion

        #region Constructor
        /// <summary>Creates a new instance of the RiptideLogger.</summary>
        /// <param name="logMethod">The method to use when logging messages.</param>
        /// <param name="includeTimestamps">Whether or not to include timestamps when logging messages.</param>
        /// <param name="timestampFormat">The format to use for timestamps.</param>
        public RiptideLogger(LogMethod logMethod, bool includeTimestamps, string timestampFormat = "HH:mm:ss")
        {
            _logMethod = logMethod;
            _includeTimestamps = includeTimestamps;
            _timestampFormat = timestampFormat;
        }
        #endregion

        #region Methods and Functions
        /// <summary>Logs a message.</summary>
        /// <param name="message">The message to log to the console.</param>
        public void Log(string message)
        {
            if (_includeTimestamps)
                _logMethod($"[{GetTimestamp(DateTime.Now)}]: {message}");
            else
                _logMethod(message);
        }
        /// <summary>Logs a message.</summary>
        /// <param name="logName">Who is logging this message.</param>
        /// <param name="message">The message to log to the console.</param>
        public void Log(string logName, string message)
        {
            if (_includeTimestamps)
                _logMethod($"[{GetTimestamp(DateTime.Now)}] ({logName}): {message}");
            else
                _logMethod($"({logName}): {message}");
        }

        /// <summary>Converts a <see cref="DateTime"/> object to a formatted timestamp string.</summary>
        /// <param name="time">The time to format.</param>
        /// <returns>The formatted timestamp.</returns>
        private string GetTimestamp(DateTime time)
        {
#if DETAILED_LOGGING
            return time.ToString("HH:mm:ss:fff");
#else
            return time.ToString(_timestampFormat);
#endif
        }
        #endregion

    }
}
