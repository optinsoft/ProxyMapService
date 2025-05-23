﻿namespace ProxyMapService.Proxy.Configurations
{
    public class Authentication(bool required = false, bool verify = false, bool setAuthentication = false, string username = "", string password = "")
    {
        public bool Required { get; private set; } = required;
        public bool Verify { get; private set; } = verify;
        public bool SetAuthentication { get; private set; } = setAuthentication;
        public string Username { get; private set; } = username;
        public string Password { get; private set; } = password;
    }
}
