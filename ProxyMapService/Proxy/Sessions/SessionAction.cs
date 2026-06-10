using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Proxy.Sessions
{
    public readonly struct SessionAction
    {
        private readonly SessionActionEnum _value;

        private SessionAction(SessionActionEnum value)
        {
            _value = value;
        }

        public static implicit operator SessionAction(ActionEnum action)
        {
            return new SessionAction((SessionActionEnum)action);
        }

        public static implicit operator SessionAction(SessionActionEnum action)
        {
            return new SessionAction(action);
        }

        public static implicit operator SessionActionEnum(SessionAction extended)
        {
            return extended._value;
        }

        public SessionActionEnum ActionValue { get { return _value; } }

        public override string ToString() => _value.ToString();
    }

    public enum SessionActionEnum
    {
        Allow = 0,
        Deny = 1,
        Bypass = 2,
        File = 3,
        SessionAPI = 4
    }
}
