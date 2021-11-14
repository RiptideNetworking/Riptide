using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking.Transports
{
    /// <summary>Defines methods, properties, and events which every transport's server and client must implement.</summary>
    public interface ICommon
    {
        /// <summary>Whether or not to output informational log messages. Error-related log messages ignore this setting.</summary>
        bool ShouldOutputInfoLogs { get; set; }

        /// <summary>Initiates handling of currently queued messages.</summary>
        /// <remarks>This should generally be called from within a regularly executed update loop (like FixedUpdate in Unity). Messages will continue to be received in between calls, but won't be handled fully until this method is executed.</remarks>
        void Tick();
    }
}
