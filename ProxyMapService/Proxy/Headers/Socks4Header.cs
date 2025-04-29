using Proxy.Network;
using System.Net;
using System.Text;

namespace ProxyMapService.Proxy.Headers
{
    public class Socks4Header
    {
        public Socks4Header(byte[] array)
        {
            Parse(this, array);
        }

        public byte Version {
            get => Bytes[0];
        }
        public byte CommandCode
        {
            get => Bytes[1];
        }
        public byte[] Bytes { get; private set; } = [0, 0, 0, 0, 0, 0, 0, 0];
        public Address? Host { get; private set; }
        public string? UserId { get; private set; }

        private static void Parse(Socks4Header self, byte[] array)
        {
            if (array.Length < 8) return;
            Array.Copy(array, 0, self.Bytes, 0, 8);
            if (array[0] != 0x04) return;
            int port = GetAddrPortFromArray(array, 2);
            byte[]? addr = GetAddrBytesFromArray(array, 4, 4);
            string? userId = null;
            if (array[1] == 1)
            {
                if (array.Length < 9) return;
                int ulen = array.Length - 9;
                if (array[8 + ulen] != 0x0) return;
                userId = Encoding.ASCII.GetString(array, 8, ulen);
            }
            else
            {
                if (array.Length != 8) return;
            }
            self.Host = new Address(addr, port);
            self.UserId = userId;
        }

        public bool IsConnectRequest(byte[] array)
        {
            return CommandCode == 1;
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
