RWStructuredBuffer<uint> outputBuffer;

[MaximallyReconverges]
[shader("compute")]
[numthreads(4, 1, 1)]
void main(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint value;
    if ((dispatchThreadID.x & 1u) == 0u)
    {
        value = 1u;
    }
    else
    {
        value = 101u;
    }
    outputBuffer[dispatchThreadID.x] = value;
}
