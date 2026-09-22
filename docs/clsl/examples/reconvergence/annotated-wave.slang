RWStructuredBuffer<uint> outputBuffer;

[MaximallyReconverges]
[shader("compute")]
[numthreads(4, 1, 1)]
void main(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    bool takeEvenPath = (dispatchThreadID.x & 1u) == 0u;
    uint activeCount;
    if (takeEvenPath)
    {
        activeCount = WaveActiveCountBits(true);
    }
    else
    {
        activeCount = 100u + WaveActiveCountBits(true);
    }
    outputBuffer[dispatchThreadID.x] = activeCount;
}
