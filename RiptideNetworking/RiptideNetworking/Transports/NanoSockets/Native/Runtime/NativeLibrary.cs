using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;

#pragma warning disable CS1591

namespace Riptide.Transports.NanoSockets
{
    [SuppressUnmanagedCodeSecurity]
    public static unsafe class NanoSockets
    {
#if __IOS__ || (UNITY_IOS && !UNITY_EDITOR)
        private const string NATIVE_LIBRARY = "__Internal";
#else
        private const string NATIVE_LIBRARY = "nanosockets";
#endif

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_initialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_initialize();

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_deinitialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern void nanosockets_deinitialize();

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_bind", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_socket_bind(long socket, NanoSocketsIPEndPoint* address);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_address_get", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_socket_get_address(long socket, NanoSocketsIPEndPoint* address);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_create", CallingConvention = CallingConvention.Cdecl)]
        public static extern long nanosockets_socket_create(int sendBufferSize, int receiveBufferSize);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_set_nonblocking", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_socket_set_nonblocking(long socket, byte nonBlocking);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_set_option", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_socket_set_option(long socket, SocketOptionLevel level, SocketOptionName optionName, int* optionValue, int optionLength);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void nanosockets_socket_destroy(ref long socket);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_send", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_socket_send(long socket, ref NanoSocketsIPEndPoint address, void* buffer, int bufferLength);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_receive", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_socket_receive(long socket, NanoSocketsIPEndPoint* address, void* buffer, int bufferLength);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_poll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_socket_poll(long socket, uint timeout);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_address_set_ip", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_set_ip(NanoSocketsIPEndPoint* address, string ip);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_address_get_ip", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_get_ip(ref NanoSocketsIPEndPoint address, void* buffer, int length);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_address_set_ip", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_set_ip(NanoSocketsIPAddress* address, string ip);

        [DllImport(NATIVE_LIBRARY, EntryPoint = "nanosockets_address_get_ip", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nanosockets_get_ip(ref NanoSocketsIPAddress address, void* buffer, int length);
    }
}
