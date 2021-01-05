using System;
using System.Threading.Tasks;

namespace Reactor.Greenhouse.Setup.Provider
{
    public abstract class BaseProvider
    {
        public Game Game { get; internal set; }

        public abstract void Setup();
        public abstract Task DownloadAsync();
        public abstract bool IsUpdateNeeded();
    }

    public class ProviderConnectionException : Exception
    {
        public BaseProvider Provider { get; }

        public ProviderConnectionException(BaseProvider provider, string message) : base(message)
        {
            Provider = provider;
        }
    }
}
