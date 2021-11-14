using RiptideNetworking.Transports;
using System.Reflection;

namespace RiptideNetworking
{
    /// <summary>Contains shared functionality for <see cref="Server"/> and <see cref="Client"/>.</summary>
    public abstract class Common
    {
        /// <inheritdoc cref="ICommon.ShouldOutputInfoLogs"/>
        public abstract bool ShouldOutputInfoLogs { get; set; }

        /// <summary>Searches the given assembly for methods with the <see cref="MessageHandlerAttribute"/> and adds them to the dictionary of handler methods.</summary>
        /// <param name="assembly">The assembly to search for methods with the <see cref="MessageHandlerAttribute"/>.</param>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building the message handlers dictionary.</param>
        protected abstract void CreateMessageHandlersDictionary(Assembly assembly, byte messageHandlerGroupId);

        /// <inheritdoc cref="ICommon.Tick"/>
        public abstract void Tick();
    }
}
