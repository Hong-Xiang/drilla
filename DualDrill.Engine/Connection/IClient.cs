using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.Connection;

public interface IClient
{
    public string Id { get; }

    public Task<IClientPeerToPeerPair> CreatePairAsync(IClient target);
}
