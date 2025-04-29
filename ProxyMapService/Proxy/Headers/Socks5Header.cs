using Proxy.Network;
using ProxyMapService.Proxy.Socks;
using System.Net;
using System.Text;

namespace ProxyMapService.Proxy.Headers
{
    public class Socks5Header
    {
        public Socks5Header(byte[] array)
        {
            Parse(this, array);
        }

        public byte Version { get; private set; }
        public byte NMethods { get; private set; }
        public byte[]? Methods { get; private set; }
        public string? Username { get; private set; }
        public string? Password { get; private set; }
        public byte Cmd { get; private set; }
        public byte Atyp { get; private set; }
        public Address? Host { get; private set; }

        private static void Parse(Socks5Header self, byte[] array)
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
            Cmd = array[1];
            Atyp = array[3];
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