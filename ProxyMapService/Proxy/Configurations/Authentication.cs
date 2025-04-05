namespace ProxyMapService.Proxy.Configurations
{
    public class Authentication(bool required, bool verify, bool setHeader, string username, string password)
    {
        public bool Required { get; private set; } = required;
        public bool Verify { get; private set; } = verify;
        public bool SetHeader { get; private set; } = setHeader;
        public string Username { get; private set; } = username;
        public string Password { get; private set; } = password;
    }
}
