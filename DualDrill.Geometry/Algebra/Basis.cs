namespace DualDrill.Geometry.Algebra;

[Flags]
public enum Basis : int
{
    s = 0,
    e1 = 1 << 0,
    e2 = 1 << 1,
    e3 = 1 << 2,
    e4 = 1 << 3,
    e5 = 1 << 4,
    e6 = 1 << 5,
    e7 = 1 << 6,
    e8 = 1 << 7,
    e9 = 1 << 8,
}
