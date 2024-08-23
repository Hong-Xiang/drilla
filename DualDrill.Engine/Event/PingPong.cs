using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.Event;

sealed record class Ping(
    DateTimeOffset TimeStamp
)
{
}

sealed record class Pong(
    DateTimeOffset TimeStamp
)
{
}
