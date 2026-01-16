using Fare;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseProxyHandler
    {
        private static string? GetUsernameWithParameters(SessionContext context, string? username, UsernameParameterList? parameterList)
        {
            if (username == null) return null;
            if (parameterList != null)
            {
                foreach (var p in parameterList)
                {
                    string? value = context.UsernameParameterResolver.ResolveParameterValue(p, context);
                    if (!String.IsNullOrEmpty(value))
                    {
                        if (p.Name != "account")
                        {
                            username += $"-{p.Name}-{value}";
                        }
                    }
                }
            }
            return username;
        }

        protected static string? GetContextProxyUsernameWithParameters(SessionContext context)
        {
            return GetUsernameWithParameters(context, context.ProxyServer?.Username, context.ProxyServer?.UsernameParameters);
        }

        protected static string? GetContextAuthenticationUsernameWithParameters(SessionContext context)
        {
            return GetUsernameWithParameters(context, context.Mapping.Authentication.Username, context.Mapping.Authentication.UsernameParameters);
        }

        protected static async Task SendHttpReply(SessionContext context, byte[] bytes)
        {
            if (context.IncomingStream == null) return;
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        protected static async Task SendSocks4Reply(SessionContext context, Socks4Command command)
        {
            if (context.IncomingStream == null) return;
            byte[] bytes = [0x0, (byte)command, 0, 0, 0, 0, 0, 0];
            if (context.Socks4 != null)
            {
                Array.Copy(context.Socks4.Bytes, 2, bytes, 2, 6);
            }
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        protected static async Task SendSocks5Reply(SessionContext context, Socks5Status status)
        {
            if (context.IncomingStream == null) return;
            byte[] bytes = [0x05, (byte)status, 0x0, 0x01, 0x0, 0x0, 0x0, 0x0, 0x10, 0x10];
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }
    }
}
