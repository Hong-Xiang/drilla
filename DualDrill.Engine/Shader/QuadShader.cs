using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.Graphics;
using ICSharpCode.Decompiler.CSharp.Syntax;
using Silk.NET.Maths;
using Silk.NET.SDL;
using System.Collections.Immutable;
using System.Numerics;
using static ICSharpCode.Decompiler.TypeSystem.ReflectionHelper;

namespace DualDrill.Engine.Shader;


public class QuadShaderReflection : ILSL.IReflection
{
    private ILSL.IShaderModuleReflection _shaderModuleReflection;
    public QuadShaderReflection()
    {
        _shaderModuleReflection = new ILSL.ShaderModuleReflection();
    }

    public ImmutableArray<GPUVertexBufferLayout>? GetVertexBufferLayout()
    {
        var vertexBufferLayoutBuilder = _shaderModuleReflection.GetVertexBufferLayoutBuilder<QuadShader.VertexInput>();
        return vertexBufferLayoutBuilder.Build();
    }

    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor(CLSL.Language.IR.ShaderModule module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptor(module);
    }

    public GPUBindGroupLayoutDescriptorBuffer? GetBindGroupLayoutDescriptorBuffer(CLSL.Language.IR.ShaderModule module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptorBuffer(module);
    }
}

public struct QuadShader : ILSL.ISharpShader
{

    [ShaderMethod]
    int ZERO()
    {
        return 0;
    }

    [ShaderMethod]
    float Length(Vector3 a)
    {
        return (float)(Math.Sqrt(Vector3.Dot(a, a)));
    }

    [ShaderMethod]
    Vector2 Fract(Vector2 a)
    {
        return new Vector2(a.X - (float)(Math.Floor(a.X)), a.Y - (float)(Math.Floor(a.Y)));
    }

    [ShaderMethod]
    Vector3 Normalize(Vector3 a)
    {
        return a / Length(a);
    }

    [ShaderMethod]
    float SmoothStep(float edge0, float edge1, float x)
    {
        // Clamp x to the [0, 1] range
        var y = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);

        // Apply the smoothstep formula
        return y * y * (3 - 2 * y);
    }

    [ShaderMethod]
    Vector3 Mix(Vector3 x, Vector3 y, float a)
    {
        return x * (1 - a) + y * a;
    }

    [ShaderMethod]
    Vector3 Pow(Vector3 x, Vector3 y)
    {
        return new Vector3((float)Math.Pow(x.X, y.X), (float)Math.Pow(x.Y, y.Y), (float)Math.Pow(x.Z, y.Z));
    }

    [ShaderMethod]
    Vector3 Sin(Vector3 a)
    {
        return new Vector3((float)Math.Sin(a.X), (float)Math.Sin(a.Y), (float)Math.Sin(a.Z));
    }

    [ShaderMethod]
    Vector3 Cos(Vector3 a)
    {
        return new Vector3((float)Math.Cos(a.X), (float)Math.Cos(a.Y), (float)Math.Cos(a.Z));
    }

    [ShaderMethod]
    Vector3 MatrixMultiplyVector(Vector3 col0, Vector3 col1, Vector3 col2, Vector3 a)
    {
        return a.X * col0 + a.Y * col1 + a.Z * col2;
    }

    [ShaderMethod]
    Vector3 Abs(Vector3 a)
    {
        return new Vector3(Math.Abs(a.X), Math.Abs(a.Y), Math.Abs(a.Z));
    }

    [ShaderMethod]
    float ndot(Vector2 a, Vector2 b)
    {
        return a.X * b.X - a.Y * b.Y;
    }

    [ShaderMethod]
    float sdBox(Vector3 p, Vector3 b)
    {
        var d = Vector3.Abs(p) - b;
        var vec = new Vector3(Math.Max(d.X, 0.0f), Math.Max(d.Y, 0.0f), Math.Max(d.Z, 0.0f));
        return Math.Min(Math.Max(d.X, Math.Max(d.Y, d.Z)), 0.0f) + Length(vec);
    }

    [ShaderMethod]
    float sdSphere(Vector3 p, float s)
    {
        return Length(p) - s;
    }

    [ShaderMethod]
    float sdPlane(Vector3 p)
    {
        return p.Y;
    }

    [ShaderMethod]
    float sdEllipsoid(Vector3 p, Vector3 r) // approximated
    {
        float k0 = Length(p / r);
        float k1 = Length(p / (r * r));
        return k0 * (k0 - 1.0f) / k1;
    }

    [ShaderMethod]
    float sdTorus(Vector3 p, Vector3 t)
    {
        return Length(new Vector3(Length(new Vector3(p.X, 0.0f, p.Z)) - t.X, p.Y, 0.0f)) - t.Y;
    }

    [ShaderMethod]
    float sdCappedTorus(Vector3 p, Vector2 sc, float ra, float rb)
    {
        var pp = p;
        pp.X = Math.Abs(p.X);
        float k = Length(new Vector3(pp.X, pp.Y, 0.0f));
        if ((sc.Y * p.X) > (sc.X * p.Y))
        {
            k = Vector2.Dot(new Vector2(pp.X, pp.Y), sc);
        }

        return (float)Math.Sqrt(Vector3.Dot(p, p) + ra * ra - 2.0f * ra * k) - rb;
    }

    [ShaderMethod]
    float sdCapsule(Vector3 p, Vector3 a, Vector3 b, float r)
    {
        Vector3 pa = p - a, ba = b - a;
        float h = (float)Math.Clamp(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba), 0.0f, 1.0f);
        return Length(pa - ba * h) - r;
    }

    [ShaderMethod]
    float sdRoundCone1(Vector3 p, float r1, float r2, float h)
    {
        Vector2 q = new Vector2(Length(new Vector3(p.X, 0.0f, p.Z)), p.Y);

        float b = (r1 - r2) / h;
        float a = (float)Math.Sqrt(1.0f - (float)(b * b));
        float k = Vector2.Dot(q, new Vector2(-b, a));

        if (k < 0.0f) return Length(new Vector3(q.X, q.Y, 0.0f)) - r1;
        var v = (q - new Vector2(0.0f, h));
        if (k > a * h) return Length(new Vector3(v.X, v.Y, 0.0f)) - r2;

        return Vector2.Dot(q, new Vector2(a, b)) - r1;
    }

    [ShaderMethod]
    float sdRoundCone2(Vector3 p, Vector3 a, Vector3 b, float r1, float r2)
    {
        // sampling independent computations (only depend on shape)
        Vector3 ba = b - a;
        float l2 = Vector3.Dot(ba, ba);
        float rr = r1 - r2;
        float a2 = l2 - rr * rr;
        float il2 = 1.0f / l2;

        // sampling dependant computations
        Vector3 pa = p - a;
        float y = Vector3.Dot(pa, ba);
        float z = y - l2;
        var vec = pa * l2 - ba * y;
        float x2 = Vector3.Dot(vec, vec);
        float y2 = y * y * l2;
        float z2 = z * z * l2;

        // single square root!
        float k = Math.Sign(rr) * rr * rr * x2;
        if (Math.Sign(z) * a2 * z2 > k) return (float)Math.Sqrt(x2 + z2) * il2 - r2;
        if (Math.Sign(y) * a2 * y2 < k) return (float)Math.Sqrt(x2 + y2) * il2 - r1;
        return ((float)Math.Sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
    }

    [ShaderMethod]
    float sdCylinder1(Vector3 p, Vector2 h)
    {
        var d = new Vector2(Length(new Vector3(p.X, 0.0f, p.Z)), p.Y);
        var c = new Vector2(Math.Abs(d.X), Math.Abs(d.Y)) - h;
        return (float)Math.Min(Math.Max(c.X, c.Y), 0.0f) + Length(new Vector3(Math.Max(c.X, 0.0f), Math.Max(c.Y, 0.0f), 0.0f));
    }

    // arbitrary orientation
    [ShaderMethod]
    float sdCylinder2(Vector3 p, Vector3 a, Vector3 b, float r)
    {
        Vector3 pa = p - a;
        Vector3 ba = b - a;
        float baba = Vector3.Dot(ba, ba);
        float paba = Vector3.Dot(pa, ba);

        float x = Length(pa * baba - ba * paba) - r * baba;
        float y = Math.Abs(paba - baba * 0.5f) - baba * 0.5f;
        float x2 = x * x;
        float y2 = y * y * baba;
        float d;
        if (Math.Max(x, y) < 0.0f)
        {
            d = -Math.Min(x2, y2);
        }
        else
        {
            float k = 0.0f;
            float l = 0.0f;
            if (x > 0.0f)
            {
                k = x2;
            }
            if (y > 0.0f)
            {
                l = y2;
            }
            d = k + l;

        }
        return Math.Sign(d) * (float)Math.Sqrt(Math.Abs(d)) / baba;
    }

    [ShaderMethod]
    float sdCappedCone1(Vector3 p, float h, float r1, float r2)
    {
        Vector2 q = new Vector2(Length(new Vector3(p.X, 0.0f, p.Z)), p.Y);

        Vector2 k1 = new Vector2(r2, h);
        Vector2 k2 = new Vector2(r2 - r1, 2.0f * h);
        float placeholder = r2;
        if (q.Y < 0.0f)
        {
            placeholder = r1;
        }
        Vector2 ca = new Vector2(q.X - Math.Min(q.X, placeholder), Math.Abs(q.Y) - h);
        Vector2 cb = q - k1 + k2 * (float)Math.Clamp(Vector2.Dot(k1 - q, k2) / Vector2.Dot(k2, k2), 0.0f, 1.0f);
        float s = 1.0f;
        if (cb.X < 0.0f && ca.Y < 0.0f)
        {
            s = -1.0f;
        }

        return s * (float)Math.Sqrt(Math.Min(Vector2.Dot(ca, ca), Vector2.Dot(cb, cb)));
    }

    [ShaderMethod]
    float sdCappedCone2(Vector3 p, Vector3 a, Vector3 b, float ra, float rb)
    {
        float rba = rb - ra;
        float baba = Vector3.Dot(b - a, b - a);
        float papa = Vector3.Dot(p - a, p - a);
        float paba = Vector3.Dot(p - a, b - a) / baba;

        float x = (float)Math.Sqrt(papa - paba * paba * baba);
        float placehoder = rb;
        if (paba < 0.5f)
        {
            placehoder = ra;
        }
        float cax = Math.Max(0.0f, x - placehoder);
        float cay = Math.Abs(paba - 0.5f) - 0.5f;

        float k = rba * rba + baba;
        float f = Math.Clamp((rba * (x - ra) + paba * baba) / k, 0.0f, 1.0f);

        float cbx = x - ra - f * rba;
        float cby = paba - f;

        float s = 1.0f;
        if (cbx < 0.0f && cay < 0.0f)
        {
            s = -1;
        }
        return s * (float)Math.Sqrt(Math.Min(cax * cax + cay * cay * baba, cbx * cbx + cby * cby * baba));
    }

    [ShaderMethod]
    float sdSolidAngle(Vector3 pos, Vector2 c, float ra)
    {
        Vector2 p = new Vector2(Length(new Vector3(pos.X, 0.0f, pos.Z)), pos.Y);
        float l = Length(new Vector3(p.X, p.Y, 0.0f)) - ra;
        var vec = p - c * (float)Math.Clamp(Vector2.Dot(p, c), 0.0, ra);
        float m = Length(new Vector3(vec.X, vec.Y, 0.0f));
        return Math.Max(l, m * Math.Sign(c.Y * p.X - c.X * p.Y));
    }

    [ShaderMethod]
    Vector2 opU(Vector2 d1, Vector2 d2)
    {
        if (d1.X < d2.X)
        {
            return d1;
        }
        return d2;
    }

    [ShaderMethod]
    Vector2 map(Vector3 pos)
    {
        //var res = new Vector2(pos.Y, 0.0f);
        //if(sdBox(pos - new Vector3(-2.0f, 0.3f, 0.25f), new Vector3(0.3f, 0.3f, 1.0f)) < res.X)
        //{
        //    res = opU(res, new Vector2(sdSphere(pos - new Vector3(-2.0f, 0.25f, 0.0f), 0.25f), 26.9f));
        //}
        Vector2 res = new Vector2(pos.Y, 0.0f);

        // bounding box
        if (sdBox(pos - new Vector3(-2.0f, 0.3f, 0.25f), new Vector3(0.3f, 0.3f, 1.0f)) < res.X)
        {
            res = opU(res, new Vector2(sdSphere(pos - new Vector3(-2.0f, 0.25f, 0.0f), 0.25f), 26.9f));
        }

        // bounding box
        if (sdBox(pos - new Vector3(0.0f, 0.3f, -1.0f), new Vector3(0.35f, 0.3f, 2.5f)) < res.X)
        {
            res = opU(res, new Vector2(sdCappedTorus((pos - new Vector3(0.0f, 0.30f, 1.0f)) * new Vector3(1, -1, 1), new Vector2(0.866025f, -0.5f), 0.25f, 0.05f), 25.0f));
            res = opU(res, new Vector2(sdCappedCone1(pos - new Vector3(0.0f, 0.25f, -2.0f), 0.25f, 0.25f, 0.1f), 13.67f));
            res = opU(res, new Vector2(sdSolidAngle(pos - new Vector3(0.0f, 0.00f, -3.0f), new Vector2(3, 4) / 5.0f, 0.4f), 49.13f));
        }

        // bounding box
        if (sdBox(pos - new Vector3(1.0f, 0.3f, -1.0f), new Vector3(0.35f, 0.3f, 2.5f)) < res.X)
        {
            res = opU(res, new Vector2(sdBox(pos - new Vector3(1.0f, 0.25f, 0.0f), new Vector3(0.3f, 0.25f, 0.1f)), 3.0f));
            res = opU(res, new Vector2(sdCapsule(pos - new Vector3(1.0f, 0.00f, -1.0f), new Vector3(-0.1f, 0.1f, -0.1f), new Vector3(0.2f, 0.4f, 0.2f), 0.1f), 31.9f));
            res = opU(res, new Vector2(sdCylinder1(pos - new Vector3(1.0f, 0.25f, -2.0f), new Vector2(0.15f, 0.25f)), 8.0f));
        }

        // bounding box
        if (sdBox(pos - new Vector3(-1.0f, 0.35f, -1.0f), new Vector3(0.35f, 0.35f, 2.5f)) < res.X)
        {

            res = opU(res, new Vector2(sdEllipsoid(pos - new Vector3(-1.0f, 0.25f, 0.0f), new Vector3(0.2f, 0.25f, 0.05f)), 43.17f));
        }

        // bounding box
        if (sdBox(pos - new Vector3(2.0f, 0.3f, -1.0f), new Vector3(0.35f, 0.3f, 2.5f)) < res.X)
        {
            res = opU(res, new Vector2(sdCylinder2(pos - new Vector3(2.0f, 0.14f, -2.0f), new Vector3(0.1f, -0.1f, 0.0f), new Vector3(-0.2f, 0.35f, 0.1f), 0.08f), 31.2f));
            res = opU(res, new Vector2(sdCappedCone2(pos - new Vector3(2.0f, 0.09f, -1.0f), new Vector3(0.1f, 0.0f, 0.0f), new Vector3(-0.2f, 0.40f, 0.1f), 0.15f, 0.05f), 46.1f));
            res = opU(res, new Vector2(sdRoundCone2(pos - new Vector3(2.0f, 0.15f, 0.0f), new Vector3(0.1f, 0.0f, 0.0f), new Vector3(-0.1f, 0.35f, 0.1f), 0.15f, 0.05f), 51.7f));
            res = opU(res, new Vector2(sdRoundCone1(pos - new Vector3(2.0f, 0.20f, 1.0f), 0.2f, 0.1f, 0.3f), 37.0f));
        }
        return res;
    }

    [ShaderMethod]
    Vector2 iBox(Vector3 ro, Vector3 rd, Vector3 rad)
    {
        var m = new Vector3(1.0f / rd.X, 1.0f / rd.Y, 1.0f / rd.Z);
        var n = m * ro;
        var k = Vector3.Abs(m) * rad;
        var t1 = -n - k;
        var t2 = -n + k;
        return new Vector2(Math.Max(Math.Max(t1.X, t1.Y), t1.Z), Math.Min(Math.Min(t2.X, t2.Y), t2.Z));
    }

    [ShaderMethod]
    Vector2 raycast(Vector3 ro, Vector3 rd)
    {
        var res = new Vector2(-1.0f, -1.0f);

        var tmin = 1.0f;
        var tmax = 20.0f;

        // raytrace floor plane
        float tp1 = (0.0f - ro.Y) / rd.Y;

        if (tp1 > 0.0f)
        {
            tmax = Math.Min(tmax, tp1);
            res = new Vector2(tp1, 1.0f);
        }
        //else return res;

        // raymarch primitives   
        var tb = iBox(ro - new Vector3(0.0f, 0.4f, -0.5f), rd, new Vector3(2.5f, 0.41f, 3.0f));
        if (tb.X < tb.Y && tb.X > 0.0F && tb.X < tmax)
        {
            //return vec2(tb.x,2.0);
            tmin = Math.Max(tb.X, tmin);
            tmax = Math.Max(tb.Y, tmax);

            var t = tmin;
            for (var i = 0; i < 70 && t < tmax; i = i + 1)
            {
                var h = map(ro + rd * t);
                if (Math.Abs(h.X) < (0.0001f * t))
                {
                    res = new Vector2(t, h.Y);
                    break;
                }
                t = t + h.X;
            }
        }
        return res;
    }

    [ShaderMethod]
    float checkersGradBox(Vector2 p, Vector2 dpdx, Vector2 dpdy)
    {
        // filter kernel
        var w = Vector2.Abs(dpdx) + Vector2.Abs(dpdy) + new Vector2(0.001f, 0.001f);
        // analytical integral (box filter)
        var i = 2.0f * (Vector2.Abs(Fract((p - 0.5f * w) * 0.5f) - new Vector2(0.5f, 0.5f)) - Vector2.Abs(Fract((p + 0.5f * w) * 0.5f) - new Vector2(0.5f, 0.5f))) / w;
        // xor pattern
        return 0.5f - 0.5f * i.X * i.Y;
    }

    [ShaderMethod]
    Vector3 calcNormal(Vector3 pos)
    {
        var e = new Vector2(1.0f, -1.0f) * 0.5773f * 0.0005f;
        return Normalize(
            new Vector3(e.X, e.Y, e.Y) * map(pos + new Vector3(e.X, e.Y, e.Y)).X +
            new Vector3(e.Y, e.Y, e.X) * map(pos + new Vector3(e.Y, e.Y, e.X)).X +
            new Vector3(e.Y, e.X, e.Y) * map(pos + new Vector3(e.Y, e.X, e.Y)).X +
        new Vector3(e.X, e.X, e.X) * map(pos + new Vector3(e.X, e.X, e.X)).X);
    }

    [ShaderMethod]
    float calcSoftshadow(Vector3 ro, Vector3 rd, float mint, float maxt)
    {
        // bounding volume
        var tp = (0.8f - ro.Y) / rd.Y;
        var tmax = maxt;
        if (tp > 0.0f)
        {
            tmax = (float)(Math.Min(tmax, tp));
        }

        var res = 1.0f;
        var t = mint;
        for (var i = ZERO(); i < 24; i++)
        {
            var h = map(ro + rd * t).X;
            var s = (float)(Math.Clamp(8.0f * h / t, 0.0f, 1.0f));
            res = Math.Min(res, s);
            t = t + (float)(Math.Clamp(h, 0.01f, 0.2f));
            if (res < 0.004f || t > tmax)
            {
                break;
            }
        }
        res = Math.Clamp(res, 0.0f, 1.0f);
        return res * res * (3.0f - 2.0f * res);
    }

    [ShaderMethod]
    Vector3 render(Vector3 ro, Vector3 rd, Vector3 rdx, Vector3 rdy)
    {
        // background
        float d = (float)(Math.Max(rd.Y, 0.0f) * 0.3f);
        var col = new Vector3(0.7f, 0.7f, 0.9f) - new Vector3(d, d, d);

        // raycast scene
        var res = raycast(ro, rd);
        var t = res.X;
        var m = res.Y;
        if (m > -0.5f)
        {
            var pos = ro + t * rd;
            var nor = new Vector3(0.0f, 1.0f, 0.0f);
            //if (m < 1.5f)
            //{
            //    nor = new Vector3(0.0f, 1.0f, 0.0f);
            //}
            if (m >= 1.5f)
            {
                nor = calcNormal(pos);
            }
            var reflection = Vector3.Reflect(rd, nor);

            // material        
            var s = new Vector3(m * 2.0f, m * 2.0f, m * 2.0f) + new Vector3(0.0f, 1.0f, 2.0f);
            col = new Vector3(0.2f, 0.2f, 0.2f) + 0.2f * new Vector3((float)Math.Sin(s.X), (float)Math.Sin(s.Y), (float)Math.Sin(s.Z));
            var ks = 1.0f;

            if (m < 1.5f)
            {
                // project pixel footprint into the plane
                var dpdx = ro.Y * (rd / rd.Y - rdx / rdx.Y);
                var dpdy = ro.Y * (rd / rd.Y - rdy / rdy.Y);

                var f = checkersGradBox(3.0F * new Vector2(pos.X, pos.Z), 3.0f * new Vector2(dpdx.X, dpdx.Z), 3.0f * new Vector2(dpdy.X, dpdy.Z));
                col = new Vector3(0.15f, 0.15f, 0.15f) + f * new Vector3(0.05f, 0.05f, 0.05f);
                ks = 0.4f;
            }

            // lighting

            var lin = new Vector3(0.0f, 0.0f, 0.0f);

            int sun = 1;
            int sky = 1;

            // sun
            if (sun == 1)
            {
                var lig = Normalize(new Vector3(-0.5f, 0.4f, -0.6f));
                var hal = Normalize(lig - rd);
                var dif = (float)Math.Clamp(Vector3.Dot(nor, lig), 0.0f, 1.0f);

                dif *= calcSoftshadow(pos, lig, 0.02f, 2.5f);
                var spe = (float)Math.Pow(Math.Clamp(Vector3.Dot(nor, hal), 0.0f, 1.0f), 16.0f);
                spe *= dif;
                spe *= 0.04f + 0.96f * (float)Math.Pow(Math.Clamp(1.0f - Vector3.Dot(hal, lig), 0.0f, 1.0f), 5.0f);

                lin += col * 2.20f * dif * new Vector3(1.30f, 1.00f, 0.70f);
                lin += 5.0f * spe * new Vector3(1.30f, 1.00f, 0.70f) * ks;
            }
            // sky
            if (sky == 1)
            {
                var dif = (float)Math.Sqrt(Math.Clamp(0.5f + 0.5f * nor.Y, 0.0f, 1.0f));
                var spe = SmoothStep(-0.2f, 0.2f, reflection.Y);
                spe *= dif;
                spe *= 0.04f + 0.96f * (float)Math.Pow(Math.Clamp(1.0f + Vector3.Dot(nor, rd), 0.0f, 1.0f), 5.0f);
                lin += col * 0.60f * dif * new Vector3(0.40f, 0.60f, 1.15f);
                lin += 2.00f * spe * new Vector3(0.40f, 0.60f, 1.30f) * ks;
            }
            col = lin;
            col = Mix(col, new Vector3(0.7f, 0.7f, 0.9f), 1.0f - (float)Math.Exp(-0.0001f * t * t * t));
        }

        return new Vector3(Math.Clamp(col.X, 0.0f, 1.0f), Math.Clamp(col.Y, 0.0f, 1.0f), Math.Clamp(col.Z, 0.0f, 1.0f));
    }
    struct Matrix3
    {
        public Vector3 col0;
        public Vector3 col1;
        public Vector3 col2;
    }

    public struct VertexInput
    {
        [Location(0)]
        public Vector2 position;
    }

    [Group(0)]
    [Binding(0)]
    [Uniform]
    Vector2 iResolution;

    [Group(0)]
    [Binding(1)]
    [Uniform]
    float iTime;

    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    Vector4 vs(VertexInput vert)
    {
        return new Vector4(vert.position.X, vert.position.Y, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    Vector4 fs([Builtin(BuiltinBinding.position)] Vector4 vertexIn)
    {
        int antialiasing = 3;
        var time = 32.0f + iTime * 1.5f;
        var fragCoord = new Vector2(iResolution.X - vertexIn.X, iResolution.Y - vertexIn.Y);
        var ta = new Vector3(0.25f, -0.75f, -0.75f);
        var ro = ta + new Vector3(4.5f * (float)Math.Cos(0.1f * time + 7.0f), 2.2f, 4.5f * (float)Math.Sin(0.1f * time + 7.0f));
        //Camera Matrix
        float cr = 0.0f;
        var cw = Normalize(ta - ro);
        var cp = new Vector3((float)Math.Sin(cr), (float)Math.Cos(cr), 0.0f);
        var cu = Normalize(Vector3.Cross(cw, cp));
        var cv = (Vector3.Cross(cu, cw));

        var tot = new Vector3(0.0f, 0.0f, 0.0f);
        for (int m = ZERO(); m < antialiasing; m++)
        {
            for (int n = ZERO(); n < antialiasing; n++)
            {
                var o = new Vector2((float)(m) * 1.0f, (float)(n) * 1.0f) / (float)(antialiasing * 1.0f) - new Vector2(0.5f, 0.5f);
                var p = new Vector2(2.0f * fragCoord.X - iResolution.X, 2.0F * fragCoord.Y - iResolution.Y) / iResolution.Y;
                // focal length
                var fl = 2.5f;

                // ray direction
                var rd = MatrixMultiplyVector(cu, cv, cw, Normalize(new Vector3(p.X, p.Y, fl)));

                // ray differentials
                var px = (2.0f * (fragCoord + new Vector2(1.0f, 0.0f)) - new Vector2(iResolution.X, iResolution.Y)) / iResolution.Y;
                var py = (2.0f * (fragCoord + new Vector2(0.0f, 1.0f)) - new Vector2(iResolution.X, iResolution.Y)) / iResolution.Y;
                var rdx = MatrixMultiplyVector(cu, cv, cw, Normalize(new Vector3(px.X, px.Y, fl)));
                var rdy = MatrixMultiplyVector(cu, cv, cw, Normalize(new Vector3(py.X, py.Y, fl)));

                var col = render(ro, rd, rdx, rdy);

                // gamma correction
                col = Pow(col, new Vector3(0.4545f, 0.4545f, 0.4545f));

                tot = tot + col;
            }
        }
        tot /= (float)(antialiasing * antialiasing * 1.0f);
        return new Vector4(tot, 1.0f);
    }
}
