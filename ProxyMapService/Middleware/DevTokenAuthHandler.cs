using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ProxyMapService.Models;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ProxyMapService.Middleware
{
    public class DevTokenAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly DevTokenProvider _tokenProvider;

        public DevTokenAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            DevTokenProvider tokenProvider)
            : base(options, logger, encoder)
        {
            _tokenProvider = tokenProvider;
        }
        
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? providedToken = null;

            if (Request.Query.TryGetValue("token", out var queryToken))
            {
                providedToken = queryToken.ToString();
            }
            else if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var headerValue = authHeader.ToString();
                if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    providedToken = headerValue.Substring("Bearer ".Length).Trim();
                }
            }

            if (string.IsNullOrEmpty(providedToken))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (providedToken == _tokenProvider.Token)
            {
                var claims = new[] {
                    new Claim(ClaimTypes.Name, "DevUser"),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            return Task.FromResult(AuthenticateResult.NoResult());
        }
    }
}
