using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Socks;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;

namespace ProxyMapService.Proxy.Headers
{
    public class Socks5Header
    {
        public Socks5Header(byte[] array)
        {
            ParseMethods(this, array);
        }

        public Socks5Header(string? username, string? password)
        {
            var methodsBytes = GetMethodsBytes(username, password);
            ParseMethods(this, methodsBytes);
            Username = username;
            Password = password;
        }

        public byte Version { get; private set; }
        public byte NMethods { get; private set; }
        public byte[]? Methods { get; private set; }
        public string? Username { get; private set; }
        public string? Password { get; private set; }
        public byte[] ConnectRequest { get; private set; } = [0x05, 0, 0, 0];
        public byte Cmd { 
            get => ConnectRequest[1]; 
        }
        public byte Atyp { 
            get => ConnectRequest[3]; 
        }
        public Address? Host { get; private set; }

        public static byte[] GetMethodsBytes(string? username, string? password)
        {
            var usernameBytes = Encoding.ASCII.GetBytes(username ?? "");
            var passwordBytes = Encoding.ASCII.GetBytes(password ?? "");
            byte ulen = (byte)usernameBytes.Length;
            byte plen = (byte)passwordBytes.Length;
            if (ulen == 0 || plen == 0)
            {
                return [0x05, 0x01, 0x0];
            }
            return [0x05, 0x02, 0x0, 0x02];
        }

        public static byte[] GetUsernamePasswordBytes(string? username, string? password)
        {
            var usernameBytes = Encoding.ASCII.GetBytes(username ?? "");
            var passwordBytes = Encoding.ASCII.GetBytes(password ?? "");
            byte ulen = (byte)usernameBytes.Length;
            byte plen = (byte)passwordBytes.Length;
            byte[] array = new byte[3 + ulen + plen];
            array[0] = 0x01;
            array[1] = ulen;
            Array.Copy(usernameBytes, 0, array, 2, ulen);
            array[2 + ulen] = plen;
            Array.Copy(passwordBytes, 0, array, 3 + ulen, plen);
            return array;
        }

        public static byte[] GetConnectRequestBytes(IPEndPoint hostEndPoint)
        {
            byte[] addressBytes, portBytes;
            portBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)hostEndPoint.Port));
            addressBytes = hostEndPoint.Address.GetAddressBytes();
            byte[] requestBytes;
            switch (hostEndPoint.Address.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    requestBytes = new byte[10];
                    requestBytes[3] = 0x01;
                    Array.Copy(addressBytes, 0, requestBytes, 4, 4);
                    Array.Copy(portBytes, 0, requestBytes, 8, 2);
                    break;
                case AddressFamily.InterNetworkV6:
                    requestBytes = new byte[22];
                    requestBytes[3] = 0x04;
                    Array.Copy(addressBytes, 0, requestBytes, 4, 16);
                    Array.Copy(portBytes, 0, requestBytes, 20, 2);
                    break;
                default:
                    throw new ArgumentException(
                        "Invalid host address family.",
                        hostEndPoint.Address.AddressFamily.ToString()
                    );
            }
            requestBytes[0] = 0x05;
            requestBytes[1] = 0x01;
            requestBytes[2] = 0x0;
            return requestBytes;
        }

        public static byte[] GetConnectRequestBytes(string host, int port)
        {
            byte[] addressBytes, portBytes;
            portBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)port));
            byte[]? requestBytes = null;
            if (IPAddress.TryParse(host, out var ip))
            {
                addressBytes = ip.GetAddressBytes();
                switch (ip.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        requestBytes = new byte[10];
                        requestBytes[3] = 0x01;
                        Array.Copy(addressBytes, 0, requestBytes, 4, 4);
                        Array.Copy(portBytes, 0, requestBytes, 8, 2);
                        break;
                    case AddressFamily.InterNetworkV6:
                        requestBytes = new byte[22];
                        requestBytes[3] = 0x04;
                        Array.Copy(addressBytes, 0, requestBytes, 4, 16);
                        Array.Copy(portBytes, 0, requestBytes, 20, 2);
                        break;
                }
            }
            if (requestBytes == null)
            {
                addressBytes = Encoding.ASCII.GetBytes(host);
                byte alen = (byte)addressBytes.Length;
                requestBytes = new byte[7 + alen];
                requestBytes[3] = 0x03;
                requestBytes[4] = alen;
                Array.Copy(addressBytes, 0, requestBytes, 5, alen);
                Array.Copy(portBytes, 0, requestBytes, 5 + alen, 2);
            }
            requestBytes[0] = 0x05;
            requestBytes[1] = 0x01;
            requestBytes[2] = 0x0;
            return requestBytes;
        }

        private static void ParseMethods(Socks5Header self, byte[] array)
        {
            if (array.Length < 1) return;
            self.Version = array[0];
            if (self.Version != 0x05) return;
            if (array.Length < 2) return;
            self.NMethods = array[1];
            if (self.NMethods == 0 || (int)self.NMethods + 2 != array.Length) return;
            self.Methods = new byte[self.NMethods];
            for (int i = 0; i < (int)self.NMethods; ++i)
            {
                self.Methods[i] = array[i + 2];
            }
        }

        public bool ParseUsernamePassword(byte[]? array)
        {
            if (array == null) return false;
            if (array.Length < 3) return false;
            if (array[0] != 0x01) return false;
            int ulen = (int)array[1];
            if (3 + ulen > array.Length) return false;
            int plen = (int)array[2 + ulen];
            if (3 + ulen + plen != array.Length) return false;
            Username = Encoding.ASCII.GetString(array, 2, ulen);
            Password = Encoding.ASCII.GetString(array, 3 + ulen, plen);
            return true;
        }

        public Socks5Status ParseConnectRequest(byte[]? array)
        {
            if (array == null) return Socks5Status.GeneralFailure;
            if (array.Length < 7) return Socks5Status.GeneralFailure;
            if (array[0] != 0x05) return Socks5Status.GeneralFailure;
            ConnectRequest = array;
            if (Cmd != 0x01) return Socks5Status.CommandNotSupported;
            byte[]? addr;
            int alen, port;
            switch (Atyp)
            {
                case 0x01:
                    if (10 != array.Length) return Socks5Status.AddressTypeNotSupported;
                    addr = GetAddrBytesFromArray(array, 4, 4);
                    port = GetAddrPortFromArray(array, 8);
                    Host = new Address(addr, port);
                    break;
                case 0x03:
                    alen = (int)array[4];
                    if (7 + alen != array.Length) return Socks5Status.AddressTypeNotSupported;
                    addr = GetAddrBytesFromArray(array, 5, alen);
                    port = GetAddrPortFromArray(array, 5 + alen);
                    Host = new Address(Encoding.ASCII.GetString(addr), port);
                    break;
                case 0x04:
                    if (22 != array.Length) return Socks5Status.AddressTypeNotSupported;
                    addr = GetAddrBytesFromArray(array, 4, 16);
                    port = GetAddrPortFromArray(array, 20);
                    Host = new Address(addr, port);
                    break;
                default:
                    return Socks5Status.AddressTypeNotSupported;
            }
            return Socks5Status.Succeeded;
        }

        private static byte[] GetAddrBytesFromArray(byte[] array, int pos, int length)
        {
            byte[] addr = new byte[length];
            Array.Copy(array, pos, addr, 0, length);
            return addr;
        }

        private static int GetAddrPortFromArray(byte[] array, int pos)
        {
            return (int)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(array, pos));
        }
    }
}