using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;
using static DualDrill.Mathematics.DMath;
using vec2 = DualDrill.Mathematics.vec2f32;
using vec3 = DualDrill.Mathematics.vec3f32;


namespace DualDrill.CLSL.Test.ShaderModule;

// Raymarching - Primitives from shader toys https://www.shadertoy.com/view/Xds3zN

public struct RaymarchingPrimitiveShader : CLSL.ISharpShader
{
    [ShaderMethod]
    static int ZERO()
    {
        return 0;
    }

    static float dot2_v2(vec2 v)
    {
        return dot(v, v);
    }

    static float dot2_v3(vec3 v)
    {
        return dot(v, v);
    }

    static float ndot(vec2 a, vec2 b)
    {
        return a.x * b.x - a.y * b.y;
    }

    //[ShaderMethod]
    static float Length(vec3f32 a)
    {
        return sqrt(dot(a, a));
    }

    //[ShaderMethod]
    static vec2f32 Fract(vec2f32 a)
    {
        return vec2(a.x - floor(a.x), a.y - floor(a.y));
    }

    //[ShaderMethod]
    vec3 Normalize(vec3 a)
    {
        return a / length(a);
    }

    //[ShaderMethod]
    static float SmoothStep(float edge0, float edge1, float x)
    {
        // Clamp x to the [0, 1] range
        var y = clamp((x - edge0) / (edge1 - edge1), 0.0f, 1.0f);

        // Apply the smoothstep formula
        return y * y * (3 - 2 * y);
    }

    //[ShaderMethod]
    static vec3 Mix(vec3 x, vec3 y, float a)
    {
        return x * (1 - a) + y * a;
    }

    //[ShaderMethod]
    static vec3 Pow(vec3 x, vec3 y)
    {
        return vec3(pow(x.x, y.x), pow(x.y, y.y), pow(x.z, y.z));
    }

    //[ShaderMethod]
    static vec3 Sin(vec3 a)
    {
        return vec3(sin(a.x), sin(a.y), sin(a.z));
    }

    //[ShaderMethod]
    static vec3 Cos(vec3 a)
    {
        return vec3(cos(a.x), cos(a.y), cos(a.z));
    }

    //[ShaderMethod]
    static vec3f32 MatrixMultiplyVector(vec3f32 col0, vec3f32 col1, vec3f32 col2, vec3f32 a)
    {
        return a.x * col0 + a.y * col1 + a.z * col2;
    }

    //[ShaderMethod]
    //vec3 Abs(vec3 a)
    //{
    //    return vec3(abs(a.x), abs(a.y), abs(a.z));
    //}

    //[ShaderMethod]
    //float ndot(vec2 a, vec2 b)
    //{
    //    return a.x * b.x - a.y * b.y;
    //}


    static float sdPlane(vec3f32 p)
    {
        return p.y;
    }

    static float sdSphere(vec3f32 p, float s)
    {
        return length(p) - s;
    }

    static float sdBox(vec3f32 p, vec3f32 b)
    {
        var d = abs(p) - b;
        return min(max(d.x, max(d.y, d.z)), 0.0f) + length(max(d, vec3(0.0f)));
    }

    static float sdBoxFrame(vec3 pa, vec3 b, float e)
    {
        var p = pa;
        p = abs(p) - b;
        vec3 q = abs(p + e) - e;

        return min(min(
                length(max(vec3(p.x, q.y, q.z), vec3(0.0f))) + min(max(p.x, max(q.y, q.z)), 0.0f),
                length(max(vec3(q.x, p.y, q.z), vec3(0.0f))) + min(max(q.x, max(p.y, q.z)), 0.0f)),
            length(max(vec3(q.x, q.y, p.z), vec3(0.0f))) + min(max(q.x, max(q.y, p.z)), 0.0f));
    }

    static float sdEllipsoid(vec3f32 p, vec3f32 r) // approximated
    {
        float k0 = length(p / r);
        float k1 = length(p / (r * r));
        return k0 * (k0 - 1.0f) / k1;
    }

    static float sdTorus(vec3 p, vec2 t)
    {
        return length(vec2(length(p.xz) - t.x, p.y)) - t.y;
    }

    static float sdCappedTorus(vec3 pa, vec2 sc, float ra, float rb)
    {
        var p = pa;
        p.x = abs(p.x);
        float k = (sc.y * p.x > sc.x * p.y) ? dot(p.xy, sc) : length(p.xy);
        return sqrt(dot(p, p) + ra * ra - 2.0f * ra * k) - rb;
    }

    static float sdHexPrism(vec3 pa, vec2 h)
    {
        var p = vec3(pa.x, pa.y, pa.z);
        vec3 q = abs(p);

        vec3 k = vec3(-0.8660254f, 0.5f, 0.57735f);
        p = abs(p);
        p = vec3(p.xy - 2.0f * min(dot(k.xy, p.xy), 0.0f) * k.xy, p.z);
        vec2 d = vec2(
            length(p.xy - vec2(clamp(p.x, -k.z * h.x, k.z * h.x), h.x)) * sign(p.y - h.x),
            p.z - h.y);
        return min(max(d.x, d.y), 0.0f) + length(max(d, vec2(0.0f)));
    }

    static float sdOctogonPrism(vec3 pa, float r, float h)
    {
        vec3 k = vec3(-0.9238795325f, // sqrt(2+sqrt(2))/2 
            0.3826834323f, // sqrt(2-sqrt(2))/2
            0.4142135623f); // sqrt(2)-1 
        // reflections
        // reflections
        var p = abs(pa);
        var v = p.xy;
        v = v - 2.0f * min(dot(vec2(k.x, k.y), v), 0.0f) * vec2(k.x, k.y);
        v = v - 2.0f * min(dot(vec2(-k.x, k.y), v), 0.0f) * vec2(-k.x, k.y);
        // polygon side
        v = v - vec2(clamp(v.x, -k.z * r, k.z * r), r);
        vec2 d = vec2(length(v) * sign(v.y), p.z - h);
        return min(max(d.x, d.y), 0.0f) + length(max(d, vec2(0.0f)));
    }

    static float sdCapsule(vec3 p, vec3 a, vec3 b, float r)
    {
        vec3 pa = p - a, ba = b - a;
        float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0f, 1.0f);
        return length(pa - ba * h) - r;
    }

    static float sdRoundCone_s1(vec3 p, float r1, float r2, float h)
    {
        vec2 q = vec2(length(p.xz), p.y);

        float b = (r1 - r2) / h;
        float a = sqrt(1.0f - b * b);
        float k = dot(q, vec2(-b, a));

        if (k < 0.0f) return length(q) - r1;
        if (k > a * h) return length(q - vec2(0.0f, h)) - r2;

        return dot(q, vec2(a, b)) - r1;
    }

    static float sdRoundCone_s2(vec3 p, vec3 a, vec3 b, float r1, float r2)
    {
        // sampling independent computations (only depend on shape)
        vec3 ba = b - a;
        float l2 = dot(ba, ba);
        float rr = r1 - r2;
        float a2 = l2 - rr * rr;
        float il2 = 1.0f / l2;

        // sampling dependant computations
        vec3 pa = p - a;
        float y = dot(pa, ba);
        float z = y - l2;
        float x2 = dot2_v3(pa * l2 - ba * y);
        float y2 = y * y * l2;
        float z2 = z * z * l2;

        // single square root!
        float k = sign(rr) * rr * rr * x2;
        if (sign(z) * a2 * z2 > k) return sqrt(x2 + z2) * il2 - r2;
        if (sign(y) * a2 * y2 < k) return sqrt(x2 + y2) * il2 - r1;
        return (sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
    }

    static float sdTriPrism(vec3 pa, vec2 ha)
    {
        float k = sqrt(3.0f);
        // h.x *= 0.5f * k;
        // TODO: correctly handle in place assignments
        var h = vec2(ha.x * 0.5f * k, ha.y);
        var p = vec3(pa.xy / h.x, pa.z);
        p.x = abs(p.x) - 1.0f;
        p.y = p.y + 1.0f / k;
        if (p.x + k * p.y > 0.0f)
        {
            p = vec3(vec2(p.x - k * p.y, -k * p.x - p.y) / 2.0f, p.z);
        }

        p.x = p.x - clamp(p.x, -2.0f, 0.0f);
        float d1 = length(p.xy) * sign(-p.y) * h.x;
        float d2 = abs(p.z) - h.y;
        return length(max(vec2(d1, d2), vec2(0.0f))) + min(max(d1, d2), 0.0f);
    }

    // TODO: support overloadding renamming
    // vertical
    static float sdCylinder1(vec3 p, vec2 h)
    {
        vec2 d = abs(vec2(length(p.xz), p.y)) - h;
        return min(max(d.x, d.y), 0.0f) + length(max(d, vec2(0.0f)));
    }

    // arbitrary orientation
    static float sdCylinder2(vec3 p, vec3 a, vec3 b, float r)
    {
        vec3 pa = p - a;
        vec3 ba = b - a;
        float baba = dot(ba, ba);
        float paba = dot(pa, ba);

        float x = length(pa * baba - ba * paba) - r * baba;
        float y = abs(paba - baba * 0.5f) - baba * 0.5f;
        float x2 = x * x;
        float y2 = y * y * baba;
        float d = (max(x, y) < 0.0f) ? -min(x2, y2) : (((x > 0.0f) ? x2 : 0.0f) + ((y > 0.0f) ? y2 : 0.0f));
        return sign(d) * sqrt(abs(d)) / baba;
    }

    // vertical
    static float sdCone(vec3 p, vec2 c, float h)
    {
        vec2 q = h * vec2(c.x, -c.y) / c.y;
        vec2 w = vec2(length(p.xz), p.y);

        vec2 a = w - q * clamp(dot(w, q) / dot(q, q), 0.0f, 1.0f);
        vec2 b = w - q * vec2(clamp(w.x / q.x, 0.0f, 1.0f), 1.0f);
        float k = sign(q.y);
        float d = min(dot(a, a), dot(b, b));
        float s = max(k * (w.x * q.y - w.y * q.x), k * (w.y - q.y));
        return sqrt(d) * sign(s);
    }

    static float sdCappedCone1(vec3 p, float h, float r1, float r2)
    {
        vec2 q = vec2(length(p.xz), p.y);

        vec2 k1 = vec2(r2, h);
        vec2 k2 = vec2(r2 - r1, 2.0f * h);
        vec2 ca = vec2(q.x - min(q.x, (q.y < 0.0f) ? r1 : r2), abs(q.y) - h);
        vec2 cb = q - k1 + k2 * clamp(dot(k1 - q, k2) / dot2_v2(k2), 0.0f, 1.0f);
        float s = (cb.x < 0.0f && ca.y < 0.0f) ? -1.0f : 1.0f;
        return s * sqrt(min(dot2_v2(ca), dot2_v2(cb)));
    }

    static float sdCappedCone2(vec3 p, vec3 a, vec3 b, float ra, float rb)
    {
        float rba = rb - ra;
        float baba = dot(b - a, b - a);
        float papa = dot(p - a, p - a);
        float paba = dot(p - a, b - a) / baba;

        float x = sqrt(papa - paba * paba * baba);

        float cax = max(0.0f, x - ((paba < 0.5f) ? ra : rb));
        float cay = abs(paba - 0.5f) - 0.5f;

        float k = rba * rba + baba;
        float f = clamp((rba * (x - ra) + paba * baba) / k, 0.0f, 1.0f);

        float cbx = x - ra - f * rba;
        float cby = paba - f;

        float s = (cbx < 0.0f && cay < 0.0f) ? -1.0f : 1.0f;

        return s * sqrt(min(cax * cax + cay * cay * baba,
            cbx * cbx + cby * cby * baba));
    }

    // c is the sin/cos of the desired cone angle
    static float sdSolidAngle(vec3 pos, vec2 c, float ra)
    {
        vec2 p = vec2(length(pos.xz), pos.y);
        float l = length(p) - ra;
        float m = length(p - c * clamp(dot(p, c), 0.0f, ra));
        return max(l, m * sign(c.y * p.x - c.x * p.y));
    }

    static float sdOctahedron0(vec3 pa, float s)
    {
        var p = abs(pa);
        float m = p.x + p.y + p.z - s;

        // exact distance
        vec3 o = min(3.0f * p - m, vec3(0.0f));
        o = max(6.0f * p - m * 2.0f - o * 3.0f + (o.x + o.y + o.z), vec3(0.0f));
        return length(p - s * o / (o.x + o.y + o.z));

        //// bound, not exact
        //return m * 0.57735027f;
    }

    static float sdOctahedron1(vec3 p, float s)
    {
        p = abs(p);
        float m = p.x + p.y + p.z - s;

        // exact distance
        vec3 q;
        if (3.0 * p.x < m) q = p.xyz;
        else if (3.0 * p.y < m) q = p.yzx;
        else if (3.0 * p.z < m) q = p.zxy;
        else return m * 0.57735027f;
        float k = clamp(0.5f * (q.z - q.y + s), 0.0f, s);
        return length(vec3(q.x, q.y - s + k, q.z - k));
    }


    static float sdPyramid(vec3 pa, float h)
    {
        float m2 = h * h + 0.25f;

        var v = pa.xz;

        // symmetry
        v = abs(v);
        v = (v.y > v.x) ? v.yx : v;
        v = v - 0.5f;

        var p = vec3(v.x, pa.y, v.y);

        // project into face plane (2D)
        vec3 q = vec3(p.z, h * p.y - 0.5f * p.x, h * p.x + 0.5f * p.y);

        float s = max(-q.x, 0.0f);
        float t = clamp((q.y - 0.5f * p.z) / (m2 + 0.25f), 0.0f, 1.0f);

        float a = m2 * (q.x + s) * (q.x + s) + q.y * q.y;
        float b = m2 * (q.x + 0.5f * t) * (q.x + 0.5f * t) + (q.y - m2 * t) * (q.y - m2 * t);

        float d2;
        if (min(q.y, -q.x * m2 - q.y * 0.5f) > 0.0f)
        {
            d2 = 0.0f;
        }
        else
        {
            d2 = min(a, b);
        }

        // recover 3D and scale, and add sign
        return sqrt((d2 + q.z * q.z) / m2) * sign(max(q.z, -p.y));
        ;
    }

    // la,lb=semi axis, h=height, ra=corner
    static float sdRhombus(vec3 pa, float la, float lb, float h, float ra)
    {
        var p = pa;
        p = abs(p);
        vec2 b = vec2(la, lb);
        float f = clamp((ndot(b, b - 2.0f * p.xz)) / dot(b, b), -1.0f, 1.0f);
        vec2 q = vec2(length(p.xz - 0.5f * b * vec2(1.0f - f, 1.0f + f)) * sign(p.x * b.y + p.z * b.x - b.x * b.y) - ra,
            p.y - h);
        return min(max(q.x, q.y), 0.0f) + length(max(q, vec2(0.0f)));
    }

    static float sdHorseshoe(vec3 pa, vec2 c, float r, float le, vec2 w)
    {
        var p = vec3(abs(pa.x), pa.yz);
        float l = length(p.xy);
        var c0 = vec2(-c.x, c.y);
        var c1 = vec2(c.y, c.x);
        //p.xy = mat2(-c.x, c.y,
        //          c.y, c.x) * p.xy;
        p = vec3(p.x * c0 + p.y * c1, p.z);
        // TODO: use ternary operator when CLSL compiler support it correctly
        //p.xy = vec2((p.y > 0.0 || p.x > 0.0) ? p.x : l * sign(-c.x),
        //(p.x > 0.0) ? p.y : l);
        {
            var px = 0.0f;
            var pxf = p.y > 0.0f || p.x > 0.0f;
            if (pxf)
            {
                px = p.x;
            }
            else
            {
                px = l * sign(-c.x);
            }

            var py = 0.0f;
            var pyf = p.x > 0.0f;
            if (pyf)
            {
                py = p.y;
            }
            else
            {
                py = l;
            }

            p.xy = vec2(px, py);
        }

        p.xy = vec2(p.x, abs(p.y - r)) - vec2(le, 0.0f);

        vec2 q = vec2(length(max(p.xy, vec2(0.0f))) + min(0.0f, max(p.x, p.y)), p.z);
        vec2 d = abs(q) - w;
        return min(max(d.x, d.y), 0.0f) + length(max(d, vec2(0.0f)));
    }

    static float sdU(vec3 p, float r, float le, vec2 w)
    {
        p.x = (p.y > 0.0) ? abs(p.x) : length(p.xy);
        p.x = abs(p.x - r);
        p.y = p.y - le;
        float k = max(p.x, p.y);
        vec2 q = vec2((k < 0.0) ? -k : length(max(p.xy, vec2(0.0f))), abs(p.z)) - w;
        return length(max(q, vec2(0.0f))) + min(max(q.x, q.y), 0.0f);
    }

    //[ShaderMethod]
    //static float sdBox(Vector3 p, Vector3 b)
    //{
    //    var d = Vector3.Abs(p) - b;
    //    var vec = new Vector3(Math.Max(d.X, 0.0f), Math.Max(d.Y, 0.0f), Math.Max(d.Z, 0.0f));
    //    return Math.Min(Math.Max(d.X, Math.Max(d.Y, d.Z)), 0.0f) + Length(vec);
    //}

    //[ShaderMethod]
    //float sdSphere(Vector3 p, float s)
    //{
    //    return Length(p) - s;
    //}

    //[ShaderMethod]
    //float sdPlane(Vector3 p)
    //{
    //    return p.Y;
    //}

    //[ShaderMethod]
    //float sdEllipsoid(Vector3 p, Vector3 r) // approximated
    //{
    //    float k0 = Length(p / r);
    //    float k1 = Length(p / (r * r));
    //    return k0 * (k0 - 1.0f) / k1;
    //}

    //[ShaderMethod]
    //float sdTorus(Vector3 p, Vector3 t)
    //{
    //    return Length(new Vector3(Length(new Vector3(p.X, 0.0f, p.Z)) - t.X, p.Y, 0.0f)) - t.Y;
    //}

    //[ShaderMethod]
    //float sdCappedTorus(Vector3 p, Vector2 sc, float ra, float rb)
    //{
    //    var pp = p;
    //    pp.X = Math.Abs(p.X);
    //    float k = Length(new Vector3(pp.X, pp.Y, 0.0f));
    //    if ((sc.Y * p.X) > (sc.X * p.Y))
    //    {
    //        k = Vector2.Dot(new Vector2(pp.X, pp.Y), sc);
    //    }

    //    return (float)Math.Sqrt(Vector3.Dot(p, p) + ra * ra - 2.0f * ra * k) - rb;
    //}

    //[ShaderMethod]
    //float sdCapsule(Vector3 p, Vector3 a, Vector3 b, float r)
    //{
    //    Vector3 pa = p - a, ba = b - a;
    //    float h = (float)Math.Clamp(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba), 0.0f, 1.0f);
    //    return Length(pa - ba * h) - r;
    //}

    //[ShaderMethod]
    //float sdRoundCone1(Vector3 p, float r1, float r2, float h)
    //{
    //    Vector2 q = new Vector2(Length(new Vector3(p.X, 0.0f, p.Z)), p.Y);

    //    float b = (r1 - r2) / h;
    //    float a = (float)Math.Sqrt(1.0f - (float)(b * b));
    //    float k = Vector2.Dot(q, new Vector2(-b, a));

    //    if (k < 0.0f) return Length(new Vector3(q.X, q.Y, 0.0f)) - r1;
    //    var v = (q - new Vector2(0.0f, h));
    //    if (k > a * h) return Length(new Vector3(v.X, v.Y, 0.0f)) - r2;

    //    return Vector2.Dot(q, new Vector2(a, b)) - r1;
    //}

    //[ShaderMethod]
    //float sdRoundCone2(Vector3 p, Vector3 a, Vector3 b, float r1, float r2)
    //{
    //    // sampling independent computations (only depend on shape)
    //    Vector3 ba = b - a;
    //    float l2 = Vector3.Dot(ba, ba);
    //    float rr = r1 - r2;
    //    float a2 = l2 - rr * rr;
    //    float il2 = 1.0f / l2;

    //    // sampling dependant computations
    //    Vector3 pa = p - a;
    //    float y = Vector3.Dot(pa, ba);
    //    float z = y - l2;
    //    var vec = pa * l2 - ba * y;
    //    float x2 = Vector3.Dot(vec, vec);
    //    float y2 = y * y * l2;
    //    float z2 = z * z * l2;

    //    // single square root!
    //    float k = Math.Sign(rr) * rr * rr * x2;
    //    if (Math.Sign(z) * a2 * z2 > k) return (float)Math.Sqrt(x2 + z2) * il2 - r2;
    //    if (Math.Sign(y) * a2 * y2 < k) return (float)Math.Sqrt(x2 + y2) * il2 - r1;
    //    return ((float)Math.Sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
    //}

    //[ShaderMethod]
    //float sdCylinder1(Vector3 p, Vector2 h)
    //{
    //    var d = new Vector2(Length(new Vector3(p.X, 0.0f, p.Z)), p.Y);
    //    var c = new Vector2(Math.Abs(d.X), Math.Abs(d.Y)) - h;
    //    return (float)Math.Min(Math.Max(c.X, c.Y), 0.0f) + Length(new Vector3(Math.Max(c.X, 0.0f), Math.Max(c.Y, 0.0f), 0.0f));
    //}

    //// arbitrary orientation
    //[ShaderMethod]
    //float sdCylinder2(Vector3 p, Vector3 a, Vector3 b, float r)
    //{
    //    Vector3 pa = p - a;
    //    Vector3 ba = b - a;
    //    float baba = Vector3.Dot(ba, ba);
    //    float paba = Vector3.Dot(pa, ba);

    //    float x = Length(pa * baba - ba * paba) - r * baba;
    //    float y = Math.Abs(paba - baba * 0.5f) - baba * 0.5f;
    //    float x2 = x * x;
    //    float y2 = y * y * baba;
    //    float d;
    //    if (Math.Max(x, y) < 0.0f)
    //    {
    //        d = -Math.Min(x2, y2);
    //    }
    //    else
    //    {
    //        float k = 0.0f;
    //        float l = 0.0f;
    //        if (x > 0.0f)
    //        {
    //            k = x2;
    //        }
    //        if (y > 0.0f)
    //        {
    //            l = y2;
    //        }
    //        d = k + l;

    //    }
    //    return Math.Sign(d) * (float)Math.Sqrt(Math.Abs(d)) / baba;
    //}

    //[ShaderMethod]
    //float sdCappedCone1(Vector3 p, float h, float r1, float r2)
    //{
    //    Vector2 q = new Vector2(Length(new Vector3(p.X, 0.0f, p.Z)), p.Y);

    //    Vector2 k1 = new Vector2(r2, h);
    //    Vector2 k2 = new Vector2(r2 - r1, 2.0f * h);
    //    float placeholder = r2;
    //    if (q.Y < 0.0f)
    //    {
    //        placeholder = r1;
    //    }
    //    Vector2 ca = new Vector2(q.X - Math.Min(q.X, placeholder), Math.Abs(q.Y) - h);
    //    Vector2 cb = q - k1 + k2 * (float)Math.Clamp(Vector2.Dot(k1 - q, k2) / Vector2.Dot(k2, k2), 0.0f, 1.0f);
    //    float s = 1.0f;
    //    if (cb.X < 0.0f && ca.Y < 0.0f)
    //    {
    //        s = -1.0f;
    //    }

    //    return s * (float)Math.Sqrt(Math.Min(Vector2.Dot(ca, ca), Vector2.Dot(cb, cb)));
    //}

    //[ShaderMethod]
    //float sdCappedCone2(Vector3 p, Vector3 a, Vector3 b, float ra, float rb)
    //{
    //    float rba = rb - ra;
    //    float baba = Vector3.Dot(b - a, b - a);
    //    float papa = Vector3.Dot(p - a, p - a);
    //    float paba = Vector3.Dot(p - a, b - a) / baba;

    //    float x = (float)Math.Sqrt(papa - paba * paba * baba);
    //    float placehoder = rb;
    //    if (paba < 0.5f)
    //    {
    //        placehoder = ra;
    //    }
    //    float cax = Math.Max(0.0f, x - placehoder);
    //    float cay = Math.Abs(paba - 0.5f) - 0.5f;

    //    float k = rba * rba + baba;
    //    float f = Math.Clamp((rba * (x - ra) + paba * baba) / k, 0.0f, 1.0f);

    //    float cbx = x - ra - f * rba;
    //    float cby = paba - f;

    //    float s = 1.0f;
    //    if (cbx < 0.0f && cay < 0.0f)
    //    {
    //        s = -1;
    //    }
    //    return s * (float)Math.Sqrt(Math.Min(cax * cax + cay * cay * baba, cbx * cbx + cby * cby * baba));
    //}

    //[ShaderMethod]
    //float sdSolidAngle(Vector3 pos, Vector2 c, float ra)
    //{
    //    Vector2 p = new Vector2(Length(new Vector3(pos.X, 0.0f, pos.Z)), pos.Y);
    //    float l = Length(new Vector3(p.X, p.Y, 0.0f)) - ra;
    //    var vec = p - c * (float)Math.Clamp(Vector2.Dot(p, c), 0.0, ra);
    //    float m = Length(new Vector3(vec.X, vec.Y, 0.0f));
    //    return Math.Max(l, m * Math.Sign(c.Y * p.X - c.X * p.Y));
    //}

    //[ShaderMethod]
    static vec2 opU(vec2 d1, vec2 d2)
    {
        if (d1.x < d2.x)
        {
            return d1;
        }

        return d2;
    }


    static vec2 map(vec3 pos)
    {
        var res = vec2(pos.y, 0.0f);
        //var res = vec2(pos.y, 0.0f);
        //return vec2(0.0f, pos.y);

        // bounding box
        if (sdBox(pos - vec3(-2.0f, 0.3f, 0.25f), vec3(0.3f, 0.3f, 1.0f)) < res.x)
        {
            res = opU(res, vec2(sdSphere(pos - vec3(-2.0f, 0.25f, 0.0f), 0.25f), 26.9f));
            //return vec2(sdSphere(pos - vec3(-2.0f, 0.25f, 0.0f), 0.25f), 26.9f);
            //return vec2(0.0f, 20.5f);
            res = opU(res, vec2(sdRhombus((pos - vec3(-2.0f, 0.25f, 1.0f)).xzy, 0.15f, 0.25f, 0.04f, 0.08f), 17.0f));
        }

        // bounding box
        if (sdBox(pos - vec3(0.0f, 0.3f, -1.0f), vec3(0.35f, 0.3f, 2.5f)) < res.x)
        {
            res = opU(res,
                vec2(
                    sdCappedTorus((pos - vec3(0.0f, 0.30f, 1.0f)) * vec3(1.0f, -1.0f, 1.0f), vec2(0.866025f, -0.5f),
                        0.25f, 0.05f), 25.0f));
            res = opU(res, vec2(sdBoxFrame(pos - vec3(0.0f, 0.25f, 0.0f), vec3(0.3f, 0.25f, 0.2f), 0.025f), 16.9f));
            res = opU(res, vec2(sdCone(pos - vec3(0.0f, 0.45f, -1.0f), vec2(0.6f, 0.8f), 0.45f), 55.0f));
            res = opU(res, vec2(sdCappedCone1(pos - vec3(0.0f, 0.25f, -2.0f), 0.25f, 0.25f, 0.1f), 13.67f));
            res = opU(res, vec2(sdSolidAngle(pos - vec3(0.0f, 0.0f, -3.0f), vec2(3.0f, 4.0f) / 5.0f, 0.4f), 49.13f));
        }

        // bounding box
        if (sdBox(pos - vec3(1.0f, 0.3f, -1.0f), vec3(0.35f, 0.3f, 2.5f)) < res.x)
        {
            res = opU(res, vec2(sdTorus((pos - vec3(1.0f, 0.30f, 1.0f)).xzy, vec2(0.25f, 0.05f)), 7.1f));
            res = opU(res, vec2(sdBox(pos - vec3(1.0f, 0.25f, 0.0f), vec3(0.3f, 0.25f, 0.1f)), 3.0f));
            res = opU(res,
                vec2(sdCapsule(pos - vec3(1.0f, 0.0f, -1.0f), vec3(-0.1f, 0.1f, -0.1f), vec3(0.2f, 0.4f, 0.2f), 0.1f),
                    31.9f));
            res = opU(res, vec2(sdCylinder1(pos - vec3(1.0f, 0.25f, -2.0f), vec2(0.15f, 0.25f)), 8.0f));
            res = opU(res, vec2(sdHexPrism(pos - vec3(1.0f, 0.2f, -3.0f), vec2(0.2f, 0.05f)), 18.4f));
        }

        // bounding box
        if (sdBox(pos - vec3(-1.0f, 0.35f, -1.0f), vec3(0.35f, 0.35f, 2.5f)) < res.x)
        {
            res = opU(res, vec2(sdPyramid(pos - vec3(-1.0f, -0.6f, -3.0f), 1.0f), 13.56f));
            res = opU(res, vec2(sdOctahedron0(pos - vec3(-1.0f, 0.15f, -2.0f), 0.35f), 23.56f));
            res = opU(res, vec2(sdTriPrism(pos - vec3(-1.0f, 0.15f, -1.0f), vec2(0.3f, 0.05f)), 43.5f));
            res = opU(res, vec2(sdEllipsoid(pos - vec3(-1.0f, 0.25f, 0.0f), vec3(0.2f, 0.25f, 0.05f)), 43.17f));
            //res = opU(res, vec2(sdHorseshoe(pos - vec3(-1.0f, 0.25f, 1.0f), vec2(cos(1.3f), sin(1.3f)), 0.2f, 0.3f, vec2(0.03f, 0.08f)), 11.5f));
        }

        // bounding box
        if (sdBox(pos - vec3(2.0f, 0.3f, -1.0f), vec3(0.35f, 0.3f, 2.5f)) < res.x)
        {
            res = opU(res, vec2(sdOctogonPrism(pos - vec3(2.0f, 0.2f, -3.0f), 0.2f, 0.05f), 51.8f));
            res = opU(res,
                vec2(
                    sdCylinder2(pos - vec3(2.0f, 0.14f, -2.0f), vec3(0.1f, -0.1f, 0.0f), vec3(-0.2f, 0.35f, 0.1f),
                        0.08f), 31.2f));
            res = opU(res,
                vec2(
                    sdCappedCone2(pos - vec3(2.0f, 0.09f, -1.0f), vec3(0.1f, 0.0f, 0.0f), vec3(-0.2f, 0.40f, 0.1f),
                        0.15f, 0.05f), 46.1f));
            res = opU(res,
                vec2(
                    sdRoundCone_s2(pos - vec3(2.0f, 0.15f, 0.0f), vec3(0.1f, 0.0f, 0.0f), vec3(-0.1f, 0.35f, 0.1f),
                        0.15f, 0.05f), 51.7f));
            res = opU(res, vec2(sdRoundCone_s1(pos - vec3(2.0f, 0.20f, 1.0f), 0.2f, 0.1f, 0.3f), 37.0f));
        }

        return res;
    }

    //[ShaderMethod]
    //vec2f32 map(vec3f32 pos)
    //{
    //    //var res = new Vector2(pos.Y, 0.0f);
    //    //if(sdBox(pos - new Vector3(-2.0f, 0.3f, 0.25f), new Vector3(0.3f, 0.3f, 1.0f)) < res.X)
    //    //{
    //    //    res = opU(res, new Vector2(sdSphere(pos - new Vector3(-2.0f, 0.25f, 0.0f), 0.25f), 26.9f));
    //    //}
    //    Vector2 res = new Vector2(pos.Y, 0.0f);

    //    // bounding box
    //    if (sdBox(pos - new Vector3(-2.0f, 0.3f, 0.25f), new Vector3(0.3f, 0.3f, 1.0f)) < res.X)
    //    {
    //        res = opU(res, new Vector2(sdSphere(pos - new Vector3(-2.0f, 0.25f, 0.0f), 0.25f), 26.9f));
    //    }

    //    // bounding box
    //    if (sdBox(pos - new Vector3(0.0f, 0.3f, -1.0f), new Vector3(0.35f, 0.3f, 2.5f)) < res.X)
    //    {
    //        res = opU(res, new Vector2(sdCappedTorus((pos - new Vector3(0.0f, 0.30f, 1.0f)) * new Vector3(1, -1, 1), new Vector2(0.866025f, -0.5f), 0.25f, 0.05f), 25.0f));
    //        res = opU(res, new Vector2(sdCappedCone1(pos - new Vector3(0.0f, 0.25f, -2.0f), 0.25f, 0.25f, 0.1f), 13.67f));
    //        res = opU(res, new Vector2(sdSolidAngle(pos - new Vector3(0.0f, 0.00f, -3.0f), new Vector2(3, 4) / 5.0f, 0.4f), 49.13f));
    //    }

    //    // bounding box
    //    if (sdBox(pos - new Vector3(1.0f, 0.3f, -1.0f), new Vector3(0.35f, 0.3f, 2.5f)) < res.X)
    //    {
    //        res = opU(res, new Vector2(sdBox(pos - new Vector3(1.0f, 0.25f, 0.0f), new Vector3(0.3f, 0.25f, 0.1f)), 3.0f));
    //        res = opU(res, new Vector2(sdCapsule(pos - new Vector3(1.0f, 0.00f, -1.0f), new Vector3(-0.1f, 0.1f, -0.1f), new Vector3(0.2f, 0.4f, 0.2f), 0.1f), 31.9f));
    //        res = opU(res, new Vector2(sdCylinder1(pos - new Vector3(1.0f, 0.25f, -2.0f), new Vector2(0.15f, 0.25f)), 8.0f));
    //    }

    //    // bounding box
    //    if (sdBox(pos - new Vector3(-1.0f, 0.35f, -1.0f), new Vector3(0.35f, 0.35f, 2.5f)) < res.X)
    //    {

    //        res = opU(res, new Vector2(sdEllipsoid(pos - new Vector3(-1.0f, 0.25f, 0.0f), new Vector3(0.2f, 0.25f, 0.05f)), 43.17f));
    //    }

    //    // bounding box
    //    if (sdBox(pos - new Vector3(2.0f, 0.3f, -1.0f), new Vector3(0.35f, 0.3f, 2.5f)) < res.X)
    //    {
    //        res = opU(res, new Vector2(sdCylinder2(pos - new Vector3(2.0f, 0.14f, -2.0f), new Vector3(0.1f, -0.1f, 0.0f), new Vector3(-0.2f, 0.35f, 0.1f), 0.08f), 31.2f));
    //        res = opU(res, new Vector2(sdCappedCone2(pos - new Vector3(2.0f, 0.09f, -1.0f), new Vector3(0.1f, 0.0f, 0.0f), new Vector3(-0.2f, 0.40f, 0.1f), 0.15f, 0.05f), 46.1f));
    //        res = opU(res, new Vector2(sdRoundCone2(pos - new Vector3(2.0f, 0.15f, 0.0f), new Vector3(0.1f, 0.0f, 0.0f), new Vector3(-0.1f, 0.35f, 0.1f), 0.15f, 0.05f), 51.7f));
    //        res = opU(res, new Vector2(sdRoundCone1(pos - new Vector3(2.0f, 0.20f, 1.0f), 0.2f, 0.1f, 0.3f), 37.0f));
    //    }
    //    return res;
    //}

    // https://iquilezles.org/articles/boxfunctions
    static vec2f32 iBox(vec3f32 ro, vec3f32 rd, vec3f32 rad)
    {
        var m = 1.0f / rd;
        var n = m * ro;
        var k = abs(m) * rad;
        var t1 = -n - k;
        var t2 = -n + k;
        return vec2(max(max(t1.x, t1.y), t1.z),
            min(min(t2.x, t2.y), t2.z));
    }
    //[ShaderMethod]
    //Vector2 iBox(Vector3 ro, Vector3 rd, Vector3 rad)
    //{
    //    var m = new Vector3(1.0f / rd.X, 1.0f / rd.Y, 1.0f / rd.Z);
    //    var n = m * ro;
    //    var k = Vector3.Abs(m) * rad;
    //    var t1 = -n - k;
    //    var t2 = -n + k;
    //    return new Vector2(Math.Max(Math.Max(t1.X, t1.Y), t1.Z), Math.Min(Math.Min(t2.X, t2.Y), t2.Z));
    //}

    [ShaderMethod]
    static vec2f32 raycast(vec3f32 ro, vec3f32 rd)
    {
        var res = vec2(-1.0f, -1.0f);

        var tmin = 1.0f;
        var tmax = 20.0f;

        // raytrace floor plane
        float tp1 = (0.0f - ro.y) / rd.y;

        if (tp1 > 0.0f)
        {
            tmax = min(tmax, tp1);
            res = vec2(tp1, 1.0f);
        }
        //else return res;

        // raymarch primitives   
        //var tb = iBox(ro - vec3(0.0f, 0.4f, -0.5f), rd, vec3(2.5f, 0.41f, 3.0f));
        var tb = iBox(ro, rd, vec3(2.5f, 0.5f, 2.5f));
        var cond = tb.x < tb.y && tb.y > 0.0f && tb.x < tmax;
        if (cond)
        {
            //return vec2(tb.y, 2.0f);

            tmin = max(tb.x, tmin);
            tmax = min(tb.y, tmax);


            var t = tmin;
            for (var i = 0; i < 70 && t < tmax; i = i + 1)
            {
                var h = map(ro + rd * t);
                //return vec2(tb.y, (rd * t).z);
                if (abs(h.x) < (0.0001f * t))
                {
                    res = vec2(t, h.y);
                    break;
                }
                t = t + h.x;
            }
            //return res;
        }
        return res;
    }

    [ShaderMethod]
    static float checkersGradBox(vec2f32 p, vec2f32 dpdx, vec2f32 dpdy)
    {
        // filter kernel
        var w = abs(dpdx) + abs(dpdy) + 0.001f;
        // analytical integral (box filter)
        var i = 2.0f * (abs(fract((p - 0.5f * w) * 0.5f) - 0.5f) - abs(fract((p + 0.5f * w) * 0.5f) - 0.5f)) / w;
        // xor pattern
        return 0.5f - 0.5f * i.x * i.y;
        //// filter kernel
        //var w = Vector2.Abs(dpdx) + Vector2.Abs(dpdy) + new Vector2(0.001f, 0.001f);
        //// analytical integral (box filter)
        //var i = 2.0f * (Vector2.Abs(Fract((p - 0.5f * w) * 0.5f) - new Vector2(0.5f, 0.5f)) - Vector2.Abs(Fract((p + 0.5f * w) * 0.5f) - new Vector2(0.5f, 0.5f))) / w;
        //// xor pattern
        //return 0.5f - 0.5f * i.X * i.Y;
    }

    [ShaderMethod]
    static vec3f32 calcNormal(vec3f32 pos)
    {
        var e = vec2(1.0f, -1.0f) * 0.5773f * 0.0005f;
        return normalize(e.xyy * map(pos + e.xyy).x +
                         e.yyx * map(pos + e.yyx).x +
                         e.yxy * map(pos + e.yxy).x +
                         e.xxx * map(pos + e.xxx).x);
        //var e = vec2(1.0f, -1.0f) * 0.5773f * 0.0005f;
        //return Normalize(
        //    e.xyy * map(pos + e.xyy).X + e.yyx * map(pos + e.yyx).X + e.yxy * map(pos + e.yxy).X + e.xxx * map
        //    new Vector3(e.X, e.Y, e.Y) * map(pos + new Vector3(e.X, e.Y, e.Y)).X +
        //    new Vector3(e.Y, e.Y, e.X) * map(pos + new Vector3(e.Y, e.Y, e.X)).X +
        //    new Vector3(e.Y, e.X, e.Y) * map(pos + new Vector3(e.Y, e.X, e.Y)).X +
        //new Vector3(e.X, e.X, e.X) * map(pos + new Vector3(e.X, e.X, e.X)).X);
        //vec3 n = vec3(0.0f);
        //for (int i = 0; i < 4; i++)
        //{
        //    vec3 e = 0.5773 * (2.0f * vec3((((i + 3) >> 1) & 1), ((i >> 1) & 1), (i & 1)) - 1.0);
        //    n += e * map(pos + 0.0005f * e).x;
        //    //if( n.x+n.y+n.z>100.0 ) break;
        //}
        //return normalize(n);
    }

    [ShaderMethod]
    static float calcSoftshadow(vec3f32 ro, vec3f32 rd, float mint, float maxt)
    {
        // bounding volume
        var tp = (0.8f - ro.y) / rd.y;
        var tmax = maxt;
        if (tp > 0.0f)
        {
            tmax = min(tmax, tp);
        }

        var res = 1.0f;
        var t = mint;
        for (var i = ZERO(); i < 24; i++)
        {
            var h = map(ro + rd * t).x;
            var s = clamp(8.0f * h / t, 0.0f, 1.0f);
            res = min(res, s);
            t = t + clamp(h, 0.01f, 0.2f);
            var cond = res < 0.004f || t > tmax;
            if (cond)
            {
                break;
            }
        }

        res = clamp(res, 0.0f, 1.0f);
        return res * res * (3.0f - 2.0f * res);
    }

    [ShaderMethod]
    static vec3f32 render(vec3f32 ro, vec3f32 rd, vec3f32 rdx, vec3f32 rdy)
    {
        // background
        float d = max(rd.y, 0.0f) * 0.3f;
        var col = vec3(0.7f, 0.7f, 0.9f) - vec3(d, d, d);

        // raycast scene
        var res = raycast(ro, rd);
        var t = res.x;
        var m = res.y;
        if (m > -0.5f)
        {
            var pos = ro + t * rd;
            var nor = vec3(0.0f, 1.0f, 0.0f);
            //if (m < 1.5f)
            //{
            //    nor = new Vector3(0.0f, 1.0f, 0.0f);
            //}
            if (m >= 1.5f)
            {
                nor = calcNormal(pos);
                //nor = vec3(0.0f, 1.0f, 0.0f);
            }

            var reflection = reflect(rd, nor);

            // material        
            var s = vec3(m * 2.0f) + vec3(0.0f, 1.0f, 2.0f);
            col = vec3(0.3f) + 0.3f * sin(s);
            var ks = 1.0f;

            if (m < 1.5f)
            {
                // project pixel footprint into the plane
                var dpdx = ro.y * (rd / rd.y - rdx / rdx.y);
                var dpdy = ro.y * (rd / rd.y - rdy / rdy.y);

                var f = checkersGradBox(3.0F * vec2(pos.x, pos.z), 3.0f * vec2(dpdx.x, dpdx.z),
                    3.0f * vec2(dpdy.x, dpdy.z));
                col = vec3(0.15f) + f * vec3(0.05f);
                ks = 0.4f;
            }
            //else
            //{
            //    col = vec3(0.5f);
            //}
            //return vec3(clamp(col.x, 0.0f, 1.0f), clamp(col.y, 0.0f, 1.0f), clamp(col.z, 0.0f, 1.0f));
            else
            {
                col = vec3(m / 10.0f);
            }
            //return col;

            // lighting

            //var lin = vec3(0.0f);
            var lin = col;

            int sun = 1;
            int sky = 1;

            // sun
            if (sun == 1)
            {
                var lig = normalize(vec3(-0.5f, 0.4f, -0.6f));
                var hal = normalize(lig - rd);
                var dif = clamp(dot(nor, lig), 0.0f, 1.0f);

                dif *= calcSoftshadow(pos, lig, 0.02f, 2.5f);

                var spe = pow(clamp(dot(nor, hal), 0.0f, 1.0f), 16.0f);
                spe *= dif;
                spe *= 0.04f + 0.96f * pow(clamp(1.0f - dot(hal, lig), 0.0f, 1.0f), 5.0f);

                lin += col * 2.20f * dif * vec3(1.30f, 1.00f, 0.70f);
                lin += 5.0f * spe * vec3(1.30f, 1.00f, 0.70f) * ks;
            }

            // sky
            if (sky == 1)
            {
                var dif = sqrt(clamp(0.5f + 0.5f * nor.y, 0.0f, 1.0f));
                var spe = SmoothStep(-0.2f, 0.2f, reflection.y);
                spe *= dif;
                spe *= 0.04f + 0.96f * pow(clamp(1.0f + dot(nor, rd), 0.0f, 1.0f), 5.0f);
                lin += col * 0.60f * dif * vec3(0.40f, 0.60f, 1.15f);
                lin += 2.00f * spe * vec3(0.40f, 0.60f, 1.30f) * ks;
            }

            col = lin;
            col = mix(col, vec3(0.7f, 0.7f, 0.9f), vec3(1.0f - exp(-0.0001f * t * t * t)));
        }

        return vec3(clamp(col.x, 0.0f, 1.0f), clamp(col.y, 0.0f, 1.0f), clamp(col.z, 0.0f, 1.0f));
    }

    struct Matrix3
    {
        public vec3f32 col0;
        public vec3f32 col1;
        public vec3f32 col2;
    }


    [Group(0)][Binding(0)][Uniform] static vec2f32 iResolution;

    [Group(0)][Binding(1)][Uniform] static float iTime;

    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    static vec4f32 vs([Location(0)] vec2f32 position)
    {
        return vec4(position.xy, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    static vec4f32 fs([Builtin(BuiltinBinding.position)] vec4f32 vertexIn)
    {
        int antialiasing = 1;
        var time = 32.0f + iTime * 1.5f;
        var fragCoord = iResolution - vertexIn.xy;
        var ta = vec3(0.25f, -0.75f, -0.75f);
        var ro = ta + vec3(4.5f * cos(0.1f * time + 7.0f), 2.2f, 4.5f * sin(0.1f * time + 7.0f));
        //Camera Matrix
        float cr = 0.0f;
        var cw = normalize(ta - ro);
        var cp = vec3(sin(cr), cos(cr), 0.0f);
        var cu = normalize(cross(cw, cp));
        var cv = cross(cu, cw);

        var tot = vec3(0.0f);
        // TODO: implement correct nested loop support
        for (var loop = 0; loop < antialiasing * antialiasing; ++loop)
        {
            var m = loop / antialiasing;
            var n = loop % antialiasing;
            // for (int m = ZERO(); m < antialiasing; m++)
            // {
            //     for (int n = ZERO(); n < antialiasing; n++)
            //     {
            var o = vec2((float)(m) * 1.0f, (float)(n) * 1.0f) / (float)(antialiasing * 1.0f) - vec2(0.5f);
            var p = vec2(2.0f * fragCoord.x - iResolution.x, 2.0F * fragCoord.y - iResolution.y) / iResolution.y;
            // focal length
            var fl = 2.5f;

            // ray direction
            var rd = MatrixMultiplyVector(cu, cv, cw, normalize(vec3(p.x, p.y, fl)));

            // ray differentials
            var px = (2.0f * (fragCoord + vec2(1.0f, 0.0f)) - vec2(iResolution.x, iResolution.y)) / iResolution.y;
            var py = (2.0f * (fragCoord + vec2(0.0f, 1.0f)) - vec2(iResolution.x, iResolution.y)) / iResolution.y;
            var rdx = MatrixMultiplyVector(cu, cv, cw, normalize(vec3(px, fl)));
            var rdy = MatrixMultiplyVector(cu, cv, cw, normalize(vec3(py, fl)));

            var col = render(ro, rd, rdx, rdy);

            // gamma correction
            col = pow(col, vec3(0.4545f));

            tot = tot + col;
            //     }
            // }
        }

        tot /= (float)(antialiasing * antialiasing * 1.0f);
        return vec4(tot, 1.0f);
    }
}