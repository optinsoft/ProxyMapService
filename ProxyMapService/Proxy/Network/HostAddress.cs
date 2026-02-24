using System;
using System.Net;

namespace ProxyMapService.Proxy.Network
{
    public class HostAddress
    {
        public HostAddress(string hostname, int port)
        {
            Hostname = hostname;
            Port = port;
        }

        public HostAddress(byte[] ipBytes, int port)
        {
            IPAddress ipAddress = new(ipBytes);
            Hostname = ipAddress.ToString();
            Port = port;
        }

        public string Hostname { get; private set; }
        public int Port { get; private set; }
        public bool Overwritten { get; private set; }

        public void Assign(HostAddress host)
        {
            Hostname = host.Hostname;
            Port = host.Port;
            Overwritten = false;
        }

        public override string ToString()
        {
            return $"{Hostname}:{Port}";
        }

        public void OverrideHostName(string hostname)
        {
            if (!string.Equals(Hostname, hostname))
            {
                Hostname = hostname;
                Overwritten = true;
            }
        }

        public void OverridePort(int port)
        {
            if (Port != port)
            {
                Port = port;
                Overwritten = true;
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