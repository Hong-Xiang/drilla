using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.Input;

public readonly record struct ClientInput<TPayload>(
    Guid ClientId,
    TPayload Payload
)
{
}
