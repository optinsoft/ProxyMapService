namespace ProxyMapService.Proxy.Configurations
{
    public class SslClientOptionsConfig
    {
        public string EnabledSslProtocols { get; set; } = "Tls12,Tls13";
        public bool CheckCertificateRevocation { get; set; } = true;
    }
}
