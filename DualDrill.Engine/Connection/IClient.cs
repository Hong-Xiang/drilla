using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.Connection;

public interface IClient : IEquatable<IClient>
{
    public string Id { get; }
    public Task<IP2PClientPair> CreatePairAsync(IClient target);
    public IObservable<IP2PClientPair> PairedAsTarget { get; }
}
