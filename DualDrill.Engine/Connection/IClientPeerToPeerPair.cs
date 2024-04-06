using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.Connection;
public interface IConnectedPair<T> : IAsyncDisposable
{
    T Source { get; }
    T Target { get; }
}

public interface IDataChannelReference
{
    Task Send<T>(T message);
}

public interface IDataChannelReferncePair : IConnectedPair<IDataChannelReference>
{
}

public interface IVideoReference
{
}

public interface IVideoChannelReference
{
}

public interface IVideoChannelReferencePair : IConnectedPair<IVideoChannelReference>
{
}

public interface IClientPeerToPeerPair
{
    IClient SourceClient { get; }
    IClient TargetClient { get; }
    Task<IDataChannelReferncePair> CreateDataChannel();
    Task<IVideoChannelReferencePair> CreateVideoChannel(IVideoChannelReference video);
}
