using System.Net;

namespace ProxyMapService.Proxy.Network
{
    public class HostAddress
    {
        private string _originalHostname = string.Empty;
        private int _originalPort;
        private string _hostname = string.Empty;
        private int _port;
        private bool _overwritten;

        public HostAddress(string hostname, int port)
        {
            _originalHostname = hostname;
            _originalPort = port;
            _hostname = _originalHostname;
            _port = _originalPort;
            _overwritten = false;
        }

        public HostAddress(byte[] ipBytes, int port)
        {
            IPAddress ipAddress = new(ipBytes);
            _originalHostname = ipAddress.ToString();
            _originalPort = port;
            _hostname = _originalHostname;
            _port = _originalPort;
            _overwritten = false;
        }

        public string OriginalHostname { get => _originalHostname; }
        public int OriginalPort { get => _originalPort; }
        public string Hostname { get => _hostname; }
        public int Port { get => _port; }
        public bool Overwritten { get => _overwritten; }

        public void Assign(HostAddress host)
        {
            _originalHostname = host.Hostname;
            _originalPort = host.Port;
            _hostname = _originalHostname;
            _port = _originalPort;
            _overwritten = false;
        }

        public override string ToString()
        {
            return $"{Hostname}:{Port}";
        }

        public void OverrideHostName(string hostname)
        {
            if (!string.Equals(Hostname, hostname))
            {
                _hostname = hostname;
                _overwritten = true;
            }
        }

        public void OverridePort(int port)
        {
            if (Port != port)
            {
                _port = port;
                _overwritten = true;
            }
        }

        protected bool Equals(HostAddress other)
        {
            return string.Equals(Hostname, other.Hostname) && Port == other.Port;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((HostAddress) obj);
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