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
