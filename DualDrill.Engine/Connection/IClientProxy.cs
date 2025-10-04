using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace DualDrill.Engine.Connection;

public interface IClientProxy<out TClient>
    where TClient : IClient
{
    TClient Client { get; }
}

public interface IClientObjectReferenceProxy<out TClient, TReference>
    : IClientProxy<TClient>
    where TClient : IClient
{
    TReference Reference { get; }
}
