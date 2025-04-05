using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public interface IHandler
    {
        Task<HandleStep> Run(SessionContext context);
    }
}
