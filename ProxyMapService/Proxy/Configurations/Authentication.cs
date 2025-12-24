namespace ProxyMapService.Proxy.Configurations
{
    public class Authentication
    {
        public bool Required { get; set; }
        public bool Verify { get; set; }
        public bool SetAuthentication { get; set; }
        public bool RemoveAuthentication { get; set; }
        public bool ParseUsernameParameters { get; set; }
        public string Username { get; set; } = String.Empty;
        public string Password { get; set; } = String.Empty;
    }
}
