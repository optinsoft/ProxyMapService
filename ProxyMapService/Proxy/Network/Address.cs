using System.Net;

namespace Proxy.Network
{
    public class Address
    {
        public Address(string hostname, int port)
        {
            Hostname = hostname;
            Port = port;
        }

        public string Hostname { get; }
        public int Port { get; }

        protected bool Equals(Address other)
        {
            return string.Equals(Hostname, other.Hostname) && Port == other.Port;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Address) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Hostname?.GetHashCode() ?? 0)*397) ^ Port;
            }
        }

        public IPEndPoint GetIPEndPoint(bool throwIfMoreThanOneIP = false)
        {
            return GetIPEndPoint(Hostname, Port, throwIfMoreThanOneIP);
        }

        public static IPEndPoint GetIPEndPoint(string hostname, int port, bool throwIfMoreThanOneIP = false)
        {
            var addresses = Dns.GetHostAddresses(hostname);
            if (addresses.Length == 0)
            {
                throw new ArgumentException(
                    "Unable to retrieve address from specified host name.",
                    hostname
                );
            }
            else if (throwIfMoreThanOneIP && addresses.Length > 1)
            {
                throw new ArgumentException(
                    "There is more that one IP address to the specified host.",
                    hostname
                );
            }
            return new IPEndPoint(addresses[0], port);
        }
    }
}