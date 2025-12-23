using Fare;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Net;
using System.Text;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseProxyHandler
    {
        protected static string? GetContextProxyUsernameWithParameters(SessionContext context)
        {
            var username = context.ProxyServer?.Username;
            if (context.ProxyServer != null) {
                foreach (var p in context.ProxyServer.UsernameParameters.Items)
                {
                    string? value = p.Value;
                    string? contextParamValue = null;
                    if (value.StartsWith('$'))
                    {
                        var contextParamName = value.Substring(1);
                        contextParamValue = context.UsernameParameters?.GetValue(contextParamName);
                        value = contextParamValue ?? p.Default;
                    }
                    if (contextParamValue == null)
                    {
                        if (value != null && value.StartsWith('^'))
                        {
                            var pattern = value.Substring(1);
                            var xeger = new Xeger(pattern);
                            value = xeger.Generate();
                        }
                    }
                    if (!String.IsNullOrEmpty(value))
                    {
                        username += $"-{p.Name}-{value}";
                    }
                }
            }
            return username;
        }

        protected static async Task SendHttpReply(SessionContext context, byte[] bytes)
        {
            if (context.ClientStream == null) return;
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }

        protected static async Task SendSocks4Reply(SessionContext context, Socks4Command command)
        {
            if (context.ClientStream == null) return;
            byte[] bytes = [0x0, (byte)command, 0, 0, 0, 0, 0, 0];
            if (context.Socks4 != null)
            {
                Array.Copy(context.Socks4.Bytes, 2, bytes, 2, 6);
            }
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }

        protected static async Task SendSocks5Reply(SessionContext context, Socks5Status status)
        {
            if (context.ClientStream == null) return;
            byte[] bytes = [0x05, (byte)status, 0x0, 0x01, 0x0, 0x0, 0x0, 0x0, 0x10, 0x10];
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }
    }
}
