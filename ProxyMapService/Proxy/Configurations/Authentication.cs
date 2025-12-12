namespace ProxyMapService.Proxy.Configurations
{
    public class Authentication(bool required = false, bool verify = false, bool setAuthentication = false, 
        bool removeAuthentication = false, string username = "", string password = "")
    {
        public bool Required { get; private set; } = required;
        public bool Verify { get; private set; } = verify;
        public bool SetAuthentication { get; private set; } = setAuthentication;
        public bool RemoveAuthentication { get; private set; } = removeAuthentication;
        public string Username { get; private set; } = username;
        public string Password { get; private set; } = password;
    }
}
