using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Test;

internal static class ScalarControlFlowFixtures
{
    public static int ConditionalReturn(int left, bool choose, int right) => choose ? left : right;

    public static bool BooleanBranch(bool choose)
    {
        if (choose)
            return false;
        return true;
    }

    public static int SharedTail(int x)
    {
        var result = 3;
        if (x > 0)
            result += 5;
        else
            result += 11;
        result = result * 7 + x;
        return result;
    }

    public static int MultipleReturns(int x)
    {
        if (x < 0)
            return x - 7;
        if (x == 0)
            return 31;
        return x + 13;
    }

    public static int ContinueAndBreak(int n)
    {
        var result = 1;
        for (var i = 0; i < n; i++)
        {
            if (i == 2)
                continue;
            if (i == 5)
                break;
            result = result * 3 + i;
        }
        return result + 17;
    }

    public static int LoopCarriedSwap(int n)
    {
        var a = 1;
        var b = 2;
        for (var i = 0; i < n; i++)
        {
            var previous = a;
            a = b;
            b = previous + i;
        }
        return a * 11 + b;
    }

    public static int NestedLoopControl(int outer, int inner)
    {
        var result = 1;
        for (var i = 0; i < outer; i++)
        {
            for (var j = 0; j < inner; j++)
            {
                if (j == 1)
                    continue;
                if (j == 4)
                    break;
                result = result * 3 + i + j;
            }
            result = result * 5 + i;
        }
        return result + 17;
    }

    public static int ThreeNestedLoops(int count)
    {
        var result = 0;
        for (var i = 0; i < count; i++)
        {
            for (var j = 0; j < count; j++)
            {
                for (var k = 0; k < count; k++)
                    result += i + j + k;
                result += 5;
            }
            result += 7;
        }
        return result + 11;
    }

    public static int EdgeValue(int x)
    {
        var result = 7;
        result += x > 0 ? x + 2 : x - 3;
        return result * 5;
    }
}

internal sealed class ScalarControlFlowShader : ISharpShader
{
    [Fragment]
    [return: Location(0)]
    public static int ScalarFragment([Location(0)] int x) =>
        ScalarControlFlowFixtures.SharedTail(x) + ScalarControlFlowFixtures.ContinueAndBreak(x);
}

internal sealed class ScalarBooleanCallShader : ISharpShader
{
    [Fragment]
    [return: Location(0)]
    public static int BooleanFragment([Location(0)] int x) =>
        ScalarControlFlowFixtures.BooleanBranch(x > 0)
            ? 1 : 2;
}
