import { GUI } from "lil-gui";
import { createRoot } from "react-dom/client";
import { InteractiveApp } from "./interactive-ui";
import { createElement } from "react";
import { uniform } from "three/examples/jsm/nodes/Nodes.js";
import { min } from "wgpu-matrix/dist/2.x/vec2-impl";

const debugCode = `
struct GlobalParams_std140_0
{
    @align(16) v_0_iTime_0 : f32,
};

@binding(0) @group(0) var<uniform> globalParams_0 : GlobalParams_std140_0;
fn MatrixMultiplyVector_0( col0_0 : vec3<f32>,  col1_0 : vec3<f32>,  col2_0 : vec3<f32>,  a_0 : vec3<f32>) -> vec3<f32>
{
    return vec3<f32>(a_0.x) * col0_0 + vec3<f32>(a_0.y) * col1_0 + vec3<f32>(a_0.z) * col2_0;
}

fn iBox_0( ro_0 : vec3<f32>,  rd_0 : vec3<f32>,  rad_0 : vec3<f32>) -> vec2<f32>
{
    var v_2681_loc_0_0 : vec3<f32> = vec3<f32>(1.0f) / rd_0;
    var v_2683_loc_2_0 : vec3<f32> = abs(v_2681_loc_0_0) * rad_0;
    var _S1 : vec3<f32> = - (v_2681_loc_0_0 * ro_0);
    var v_2684_loc_3_0 : vec3<f32> = _S1 - v_2683_loc_2_0;
    var v_2685_loc_4_0 : vec3<f32> = _S1 + v_2683_loc_2_0;
    return vec2<f32>(max(max(v_2684_loc_3_0.x, v_2684_loc_3_0.y), v_2684_loc_3_0.z), min(min(v_2685_loc_4_0.x, v_2685_loc_4_0.y), v_2685_loc_4_0.z));
}

fn sdBox_0( p_0 : vec3<f32>,  b_0 : vec3<f32>) -> f32
{
    var v_479_loc_0_0 : vec3<f32> = abs(p_0) - b_0;
    return min(max(v_479_loc_0_0.x, max(v_479_loc_0_0.y, v_479_loc_0_0.z)), 0.0f) + length(max(v_479_loc_0_0, vec3<f32>(0.0f)));
}

fn sdSphere_0( p_1 : vec3<f32>,  s_0 : f32) -> f32
{
    return length(p_1) - s_0;
}

fn opU_0( d1_0 : vec2<f32>,  d2_0 : vec2<f32>) -> vec2<f32>
{
    var v_504_loc_1_0 : vec2<f32>;
    if(bool(i32(bool(i32((d1_0.x) < (d2_0.x))))))
    {
        v_504_loc_1_0 = d1_0;
    }
    else
    {
        v_504_loc_1_0 = d2_0;
    }
    return v_504_loc_1_0;
}

fn ndot_0( a_1 : vec2<f32>,  b_1 : vec2<f32>) -> f32
{
    return a_1.x * b_1.x - a_1.y * b_1.y;
}

fn sdRhombus_0( pa_0 : vec3<f32>,  la_0 : f32,  lb_0 : f32,  h_0 : f32,  ra_0 : f32) -> f32
{
    var v_518_loc_0_0 : vec3<f32> = abs(pa_0);
    var v_519_loc_1_0 : vec2<f32> = vec2<f32>(la_0, lb_0);
    var _S2 : vec2<f32> = v_518_loc_0_0.xz;
    var v_520_loc_2_0 : f32 = clamp(ndot_0(v_519_loc_1_0, v_519_loc_1_0 - vec2<f32>(2.0f) * _S2) / dot(v_519_loc_1_0, v_519_loc_1_0), -1.0f, 1.0f);
    var _S3 : f32 = length(_S2 - vec2<f32>(0.5f) * v_519_loc_1_0 * vec2<f32>(1.0f - v_520_loc_2_0, 1.0f + v_520_loc_2_0)) * f32(sign(v_518_loc_0_0.x * lb_0 + v_518_loc_0_0.z * la_0 - la_0 * lb_0)) - ra_0;
    var _S4 : f32 = v_518_loc_0_0.y - h_0;
    return min(max(_S3, _S4), 0.0f) + length(max(vec2<f32>(_S3, _S4), vec2<f32>(0.0f)));
}

fn sdCappedTorus_0( pa_1 : vec3<f32>,  sc_0 : vec2<f32>,  ra_1 : f32,  rb_0 : f32) -> f32
{
    var v_599_loc_0_0 : vec3<f32> = pa_1;
    v_599_loc_0_0[i32(0)] = abs(pa_1.x);
    var v_597_0 : f32;
    if((sc_0.y * v_599_loc_0_0.x) > (sc_0.x * v_599_loc_0_0.y))
    {
        v_597_0 = dot(v_599_loc_0_0.xy, sc_0);
    }
    else
    {
        v_597_0 = length(v_599_loc_0_0.xy);
    }
    return sqrt(dot(v_599_loc_0_0, v_599_loc_0_0) + ra_1 * ra_1 - 2.0f * ra_1 * v_597_0) - rb_0;
}

fn sdBoxFrame_0( pa_2 : vec3<f32>,  b_2 : vec3<f32>,  e_0 : f32) -> f32
{
    var v_635_loc_0_0 : vec3<f32> = abs(pa_2) - b_2;
    var _S5 : vec3<f32> = vec3<f32>(e_0);
    var v_636_loc_1_0 : vec3<f32> = abs(v_635_loc_0_0 + _S5) - _S5;
    var _S6 : f32 = v_635_loc_0_0.x;
    var _S7 : f32 = v_636_loc_1_0.y;
    var _S8 : f32 = v_636_loc_1_0.z;
    var _S9 : vec3<f32> = vec3<f32>(0.0f);
    var _S10 : f32 = v_636_loc_1_0.x;
    var _S11 : f32 = v_635_loc_0_0.y;
    var _S12 : f32 = v_635_loc_0_0.z;
    return min(min(length(max(vec3<f32>(_S6, _S7, _S8), _S9)) + min(max(_S6, max(_S7, _S8)), 0.0f), length(max(vec3<f32>(_S10, _S11, _S8), _S9)) + min(max(_S10, max(_S11, _S8)), 0.0f)), length(max(vec3<f32>(_S10, _S7, _S12), _S9)) + min(max(_S10, max(_S7, _S12)), 0.0f));
}

fn sdCone_0( p_2 : vec3<f32>,  c_0 : vec2<f32>,  h_1 : f32) -> f32
{
    var _S13 : f32 = c_0.y;
    var v_700_loc_0_0 : vec2<f32> = vec2<f32>(h_1) * vec2<f32>(c_0.x, - _S13) / vec2<f32>(_S13);
    var _S14 : f32 = length(p_2.xz);
    var _S15 : f32 = p_2.y;
    var v_701_loc_1_0 : vec2<f32> = vec2<f32>(_S14, _S15);
    var v_702_loc_2_0 : vec2<f32> = v_701_loc_1_0 - v_700_loc_0_0 * vec2<f32>(clamp(dot(v_701_loc_1_0, v_700_loc_0_0) / dot(v_700_loc_0_0, v_700_loc_0_0), 0.0f, 1.0f));
    var _S16 : f32 = v_700_loc_0_0.x;
    var v_703_loc_3_0 : vec2<f32> = v_701_loc_1_0 - v_700_loc_0_0 * vec2<f32>(clamp(_S14 / _S16, 0.0f, 1.0f), 1.0f);
    var _S17 : f32 = v_700_loc_0_0.y;
    var v_704_loc_4_0 : f32 = f32(sign(_S17));
    return sqrt(min(dot(v_702_loc_2_0, v_702_loc_2_0), dot(v_703_loc_3_0, v_703_loc_3_0))) * f32(sign(max(v_704_loc_4_0 * (_S14 * _S17 - _S15 * _S16), v_704_loc_4_0 * (_S15 - _S17))));
}

fn dot2_v2_0( v_0 : vec2<f32>) -> f32
{
    return dot(v_0, v_0);
}

fn sdCappedCone1_0( p_3 : vec3<f32>,  h_2 : f32,  r1_0 : f32,  r2_0 : f32) -> f32
{
    var _S18 : f32 = length(p_3.xz);
    var _S19 : f32 = p_3.y;
    var v_783_loc_0_0 : vec2<f32> = vec2<f32>(_S18, _S19);
    var v_784_loc_1_0 : vec2<f32> = vec2<f32>(r2_0, h_2);
    var v_785_loc_2_0 : vec2<f32> = vec2<f32>(r2_0 - r1_0, 2.0f * h_2);
    var v_782_0 : f32;
    if(_S19 < 0.0f)
    {
        v_782_0 = r1_0;
    }
    else
    {
        v_782_0 = r2_0;
    }
    var _S20 : f32 = abs(_S19) - h_2;
    var v_778_loc_3_0 : vec2<f32> = vec2<f32>(_S18 - min(_S18, v_782_0), _S20);
    var v_779_loc_4_0 : vec2<f32> = v_783_loc_0_0 - v_784_loc_1_0 + v_785_loc_2_0 * vec2<f32>(clamp(dot(v_784_loc_1_0 - v_783_loc_0_0, v_785_loc_2_0) / dot2_v2_0(v_785_loc_2_0), 0.0f, 1.0f));
    var v_776_0 : f32;
    if((v_779_loc_4_0.x) >= 0.0f)
    {
        v_776_0 = 1.0f;
    }
    else
    {
        if(_S20 < 0.0f)
        {
            v_776_0 = -1.0f;
        }
        else
        {
            v_776_0 = 1.0f;
        }
    }
    return v_776_0 * sqrt(min(dot2_v2_0(v_778_loc_3_0), dot2_v2_0(v_779_loc_4_0)));
}

fn sdSolidAngle_0( pos_0 : vec3<f32>,  c_1 : vec2<f32>,  ra_2 : f32) -> f32
{
    var _S21 : f32 = length(pos_0.xz);
    var _S22 : f32 = pos_0.y;
    var v_869_loc_0_0 : vec2<f32> = vec2<f32>(_S21, _S22);
    return max(length(v_869_loc_0_0) - ra_2, length(v_869_loc_0_0 - c_1 * vec2<f32>(clamp(dot(v_869_loc_0_0, c_1), 0.0f, ra_2))) * f32(sign(c_1.y * _S21 - c_1.x * _S22)));
}

fn sdTorus_0( p_4 : vec3<f32>,  t_0 : vec2<f32>) -> f32
{
    return length(vec2<f32>(length(p_4.xz) - t_0.x, p_4.y)) - t_0.y;
}

fn sdCapsule_0( p_5 : vec3<f32>,  a_2 : vec3<f32>,  b_3 : vec3<f32>,  r_0 : f32) -> f32
{
    var v_916_loc_0_0 : vec3<f32> = p_5 - a_2;
    var v_917_loc_1_0 : vec3<f32> = b_3 - a_2;
    return length(v_916_loc_0_0 - v_917_loc_1_0 * vec3<f32>(clamp(dot(v_916_loc_0_0, v_917_loc_1_0) / dot(v_917_loc_1_0, v_917_loc_1_0), 0.0f, 1.0f))) - r_0;
}

fn sdCylinder1_0( p_6 : vec3<f32>,  h_3 : vec2<f32>) -> f32
{
    var v_945_loc_0_0 : vec2<f32> = abs(vec2<f32>(length(p_6.xz), p_6.y)) - h_3;
    return min(max(v_945_loc_0_0.x, v_945_loc_0_0.y), 0.0f) + length(max(v_945_loc_0_0, vec2<f32>(0.0f)));
}

fn sdHexPrism_0( pa_3 : vec3<f32>,  h_4 : vec2<f32>) -> f32
{
    var v_966_loc_0_0 : vec3<f32> = abs(vec3<f32>(pa_3.x, pa_3.y, pa_3.z));
    var _S23 : vec2<f32> = v_966_loc_0_0.xy;
    const _S24 : vec2<f32> = vec2<f32>(-0.86602538824081421f, 0.5f);
    var v_966_loc_0_1 : vec3<f32> = vec3<f32>(_S23 - vec2<f32>((2.0f * min(dot(_S24, _S23), 0.0f))) * _S24, v_966_loc_0_0.z);
    var _S25 : f32 = h_4.x;
    var _S26 : f32 = length(v_966_loc_0_1.xy - vec2<f32>(clamp(v_966_loc_0_1.x, -0.57735002040863037f * _S25, 0.57735002040863037f * _S25), _S25)) * f32(sign(v_966_loc_0_1.y - _S25));
    var _S27 : f32 = v_966_loc_0_1.z - h_4.y;
    return min(max(_S26, _S27), 0.0f) + length(max(vec2<f32>(_S26, _S27), vec2<f32>(0.0f)));
}

fn sdPyramid_0( pa_4 : vec3<f32>,  h_5 : f32) -> f32
{
    var v_1033_loc_0_0 : f32 = h_5 * h_5 + 0.25f;
    var v_1038_loc_1_0 : vec2<f32> = abs(pa_4.xz);
    var v_1037_0 : vec2<f32>;
    if((v_1038_loc_1_0.y) > (v_1038_loc_1_0.x))
    {
        v_1037_0 = v_1038_loc_1_0.yx;
    }
    else
    {
        v_1037_0 = v_1038_loc_1_0;
    }
    var v_1038_loc_1_1 : vec2<f32> = v_1037_0 - vec2<f32>(0.5f);
    var _S28 : f32 = v_1038_loc_1_1.x;
    var _S29 : f32 = pa_4.y;
    var _S30 : f32 = v_1038_loc_1_1.y;
    var _S31 : f32 = h_5 * _S29 - 0.5f * _S28;
    var _S32 : f32 = h_5 * _S28 + 0.5f * _S29;
    var _S33 : f32 = - _S30;
    var v_1040_loc_5_0 : f32 = clamp((_S31 - 0.5f * _S30) / (v_1033_loc_0_0 + 0.25f), 0.0f, 1.0f);
    var _S34 : f32 = _S30 + max(_S33, 0.0f);
    var v_1035_loc_6_0 : f32 = v_1033_loc_0_0 * _S34 * _S34 + _S31 * _S31;
    var _S35 : f32 = _S30 + 0.5f * v_1040_loc_5_0;
    var _S36 : f32 = _S31 - v_1033_loc_0_0 * v_1040_loc_5_0;
    var v_1036_loc_7_0 : f32 = v_1033_loc_0_0 * _S35 * _S35 + _S36 * _S36;
    var v_1031_loc_8_0 : f32;
    if(bool(i32(bool(i32((min(_S31, _S33 * v_1033_loc_0_0 - _S31 * 0.5f)) > 0.0f)))))
    {
        v_1031_loc_8_0 = 0.0f;
    }
    else
    {
        v_1031_loc_8_0 = min(v_1035_loc_6_0, v_1036_loc_7_0);
    }
    return sqrt((v_1031_loc_8_0 + _S32 * _S32) / v_1033_loc_0_0) * f32(sign(max(_S32, - _S29)));
}

fn sdOctahedron0_0( pa_5 : vec3<f32>,  s_1 : f32) -> f32
{
    var v_1171_loc_0_0 : vec3<f32> = abs(pa_5);
    var v_1172_loc_1_0 : f32 = v_1171_loc_0_0.x + v_1171_loc_0_0.y + v_1171_loc_0_0.z - s_1;
    var _S37 : vec3<f32> = vec3<f32>(3.0f);
    var _S38 : vec3<f32> = vec3<f32>(0.0f);
    var v_1173_loc_2_0 : vec3<f32> = min(_S37 * v_1171_loc_0_0 - vec3<f32>(v_1172_loc_1_0), _S38);
    var v_1173_loc_2_1 : vec3<f32> = max(vec3<f32>(6.0f) * v_1171_loc_0_0 - vec3<f32>((v_1172_loc_1_0 * 2.0f)) - v_1173_loc_2_0 * _S37 + vec3<f32>((v_1173_loc_2_0.x + v_1173_loc_2_0.y + v_1173_loc_2_0.z)), _S38);
    return length(v_1171_loc_0_0 - vec3<f32>(s_1) * v_1173_loc_2_1 / vec3<f32>((v_1173_loc_2_1.x + v_1173_loc_2_1.y + v_1173_loc_2_1.z)));
}

fn sdTriPrism_0( pa_6 : vec3<f32>,  ha_0 : vec2<f32>) -> f32
{
    var v_1229_loc_0_0 : f32 = sqrt(3.0f);
    var _S39 : f32 = ha_0.x * 0.5f * v_1229_loc_0_0;
    var _S40 : f32 = ha_0.y;
    var _S41 : vec3<f32> = vec3<f32>(pa_6.xy / vec2<f32>(_S39), pa_6.z);
    var v_1225_loc_2_0 : vec3<f32> = _S41;
    v_1225_loc_2_0[i32(0)] = abs(_S41.x) - 1.0f;
    v_1225_loc_2_0[i32(1)] = v_1225_loc_2_0.y + 1.0f / v_1229_loc_0_0;
    if(bool(i32(bool(i32((v_1225_loc_2_0.x + v_1229_loc_0_0 * v_1225_loc_2_0.y) > 0.0f)))))
    {
        v_1225_loc_2_0 = vec3<f32>(vec2<f32>(v_1225_loc_2_0.x - v_1229_loc_0_0 * v_1225_loc_2_0.y, - v_1229_loc_0_0 * v_1225_loc_2_0.x - v_1225_loc_2_0.y) / vec2<f32>(2.0f), v_1225_loc_2_0.z);
    }
    v_1225_loc_2_0[i32(0)] = v_1225_loc_2_0.x - clamp(v_1225_loc_2_0.x, -2.0f, 0.0f);
    var v_1227_loc_3_0 : f32 = length(v_1225_loc_2_0.xy) * f32(sign(- v_1225_loc_2_0.y)) * _S39;
    var v_1228_loc_4_0 : f32 = abs(v_1225_loc_2_0.z) - _S40;
    return length(max(vec2<f32>(v_1227_loc_3_0, v_1228_loc_4_0), vec2<f32>(0.0f))) + min(max(v_1227_loc_3_0, v_1228_loc_4_0), 0.0f);
}

fn sdEllipsoid_0( p_7 : vec3<f32>,  r_1 : vec3<f32>) -> f32
{
    var v_1315_loc_0_0 : f32 = length(p_7 / r_1);
    return v_1315_loc_0_0 * (v_1315_loc_0_0 - 1.0f) / length(p_7 / (r_1 * r_1));
}

fn sdOctogonPrism_0( pa_7 : vec3<f32>,  r_2 : f32,  h_6 : f32) -> f32
{
    var v_1337_loc_1_0 : vec3<f32> = abs(pa_7);
    var v_1338_loc_2_0 : vec2<f32> = v_1337_loc_1_0.xy;
    const _S42 : vec2<f32> = vec2<f32>(-0.92387950420379639f, 0.38268342614173889f);
    var v_1338_loc_2_1 : vec2<f32> = v_1338_loc_2_0 - vec2<f32>((2.0f * min(dot(_S42, v_1338_loc_2_0), 0.0f))) * _S42;
    const _S43 : vec2<f32> = vec2<f32>(0.92387950420379639f, 0.38268342614173889f);
    var v_1338_loc_2_2 : vec2<f32> = v_1338_loc_2_1 - vec2<f32>((2.0f * min(dot(_S43, v_1338_loc_2_1), 0.0f))) * _S43;
    var v_1338_loc_2_3 : vec2<f32> = v_1338_loc_2_2 - vec2<f32>(clamp(v_1338_loc_2_2.x, -0.41421356797218323f * r_2, 0.41421356797218323f * r_2), r_2);
    var _S44 : f32 = length(v_1338_loc_2_3) * f32(sign(v_1338_loc_2_3.y));
    var _S45 : f32 = v_1337_loc_1_0.z - h_6;
    return min(max(_S44, _S45), 0.0f) + length(max(vec2<f32>(_S44, _S45), vec2<f32>(0.0f)));
}

fn sdCylinder2_0( p_8 : vec3<f32>,  a_3 : vec3<f32>,  b_4 : vec3<f32>,  r_3 : f32) -> f32
{
    var v_1426_loc_0_0 : vec3<f32> = p_8 - a_3;
    var v_1427_loc_1_0 : vec3<f32> = b_4 - a_3;
    var v_1416_loc_2_0 : f32 = dot(v_1427_loc_1_0, v_1427_loc_1_0);
    var v_1428_loc_3_0 : f32 = dot(v_1426_loc_0_0, v_1427_loc_1_0);
    var v_1425_loc_4_0 : f32 = length(v_1426_loc_0_0 * vec3<f32>(v_1416_loc_2_0) - v_1427_loc_1_0 * vec3<f32>(v_1428_loc_3_0)) - r_3 * v_1416_loc_2_0;
    var _S46 : f32 = v_1416_loc_2_0 * 0.5f;
    var v_1424_loc_5_0 : f32 = abs(v_1428_loc_3_0 - _S46) - _S46;
    var v_1417_loc_6_0 : f32 = v_1425_loc_4_0 * v_1425_loc_4_0;
    var v_1418_loc_7_0 : f32 = v_1424_loc_5_0 * v_1424_loc_5_0 * v_1416_loc_2_0;
    var v_1414_0 : f32;
    if((max(v_1425_loc_4_0, v_1424_loc_5_0)) < 0.0f)
    {
        v_1414_0 = - min(v_1417_loc_6_0, v_1418_loc_7_0);
    }
    else
    {
        if(v_1425_loc_4_0 > 0.0f)
        {
            v_1414_0 = v_1417_loc_6_0;
        }
        else
        {
            v_1414_0 = 0.0f;
        }
        var v_1420_0 : f32;
        if(v_1424_loc_5_0 > 0.0f)
        {
            v_1420_0 = v_1418_loc_7_0;
        }
        else
        {
            v_1420_0 = 0.0f;
        }
        v_1414_0 = v_1414_0 + v_1420_0;
    }
    return f32(sign(v_1414_0)) * sqrt(abs(v_1414_0)) / v_1416_loc_2_0;
}

fn sdCappedCone2_0( p_9 : vec3<f32>,  a_4 : vec3<f32>,  b_5 : vec3<f32>,  ra_3 : f32,  rb_1 : f32) -> f32
{
    var v_1521_loc_0_0 : f32 = rb_1 - ra_3;
    var _S47 : vec3<f32> = b_5 - a_4;
    var v_1514_loc_1_0 : f32 = dot(_S47, _S47);
    var _S48 : vec3<f32> = p_9 - a_4;
    var v_1520_loc_3_0 : f32 = dot(_S48, _S47) / v_1514_loc_1_0;
    var v_1523_loc_4_0 : f32 = sqrt(dot(_S48, _S48) - v_1520_loc_3_0 * v_1520_loc_3_0 * v_1514_loc_1_0);
    var v_1519_0 : f32;
    if(v_1520_loc_3_0 < 0.5f)
    {
        v_1519_0 = ra_3;
    }
    else
    {
        v_1519_0 = rb_1;
    }
    var v_1512_loc_5_0 : f32 = max(0.0f, v_1523_loc_4_0 - v_1519_0);
    var v_1513_loc_6_0 : f32 = abs(v_1520_loc_3_0 - 0.5f) - 0.5f;
    var _S49 : f32 = v_1523_loc_4_0 - ra_3;
    var v_1524_loc_8_0 : f32 = clamp((v_1521_loc_0_0 * _S49 + v_1520_loc_3_0 * v_1514_loc_1_0) / (v_1521_loc_0_0 * v_1521_loc_0_0 + v_1514_loc_1_0), 0.0f, 1.0f);
    var v_1515_loc_9_0 : f32 = _S49 - v_1524_loc_8_0 * v_1521_loc_0_0;
    var v_1516_loc_10_0 : f32 = v_1520_loc_3_0 - v_1524_loc_8_0;
    var v_1510_0 : f32;
    if(v_1515_loc_9_0 >= 0.0f)
    {
        v_1510_0 = 1.0f;
    }
    else
    {
        if(v_1513_loc_6_0 < 0.0f)
        {
            v_1510_0 = -1.0f;
        }
        else
        {
            v_1510_0 = 1.0f;
        }
    }
    return v_1510_0 * sqrt(min(v_1512_loc_5_0 * v_1512_loc_5_0 + v_1513_loc_6_0 * v_1513_loc_6_0 * v_1514_loc_1_0, v_1515_loc_9_0 * v_1515_loc_9_0 + v_1516_loc_10_0 * v_1516_loc_10_0 * v_1514_loc_1_0));
}

fn dot2_v3_0( v_1 : vec3<f32>) -> f32
{
    return dot(v_1, v_1);
}

fn sdRoundCone_s2_0( p_10 : vec3<f32>,  a_5 : vec3<f32>,  b_6 : vec3<f32>,  r1_1 : f32,  r2_1 : f32) -> f32
{
    var v_1660_loc_0_0 : vec3<f32> = b_6 - a_5;
    var v_1661_loc_1_0 : f32 = dot(v_1660_loc_0_0, v_1660_loc_0_0);
    var v_1655_loc_2_0 : f32 = r1_1 - r2_1;
    var v_1652_loc_3_0 : f32 = v_1661_loc_1_0 - v_1655_loc_2_0 * v_1655_loc_2_0;
    var v_1653_loc_4_0 : f32 = 1.0f / v_1661_loc_1_0;
    var v_1662_loc_5_0 : vec3<f32> = p_10 - a_5;
    var v_1654_loc_6_0 : f32 = dot(v_1662_loc_5_0, v_1660_loc_0_0);
    var v_1663_loc_7_0 : f32 = v_1654_loc_6_0 - v_1661_loc_1_0;
    var v_1651_loc_8_0 : f32 = dot2_v3_0(v_1662_loc_5_0 * vec3<f32>(v_1661_loc_1_0) - v_1660_loc_0_0 * vec3<f32>(v_1654_loc_6_0));
    var v_1656_loc_9_0 : f32 = v_1654_loc_6_0 * v_1654_loc_6_0 * v_1661_loc_1_0;
    var v_1659_loc_10_0 : f32 = v_1663_loc_7_0 * v_1663_loc_7_0 * v_1661_loc_1_0;
    var v_1657_loc_11_0 : f32 = f32(sign(v_1655_loc_2_0)) * v_1655_loc_2_0 * v_1655_loc_2_0 * v_1651_loc_8_0;
    var v_1650_loc_13_0 : f32;
    if(bool(i32(bool(i32((f32(sign(v_1663_loc_7_0)) * v_1652_loc_3_0 * v_1659_loc_10_0) > v_1657_loc_11_0)))))
    {
        v_1650_loc_13_0 = sqrt(v_1651_loc_8_0 + v_1659_loc_10_0) * v_1653_loc_4_0 - r2_1;
    }
    else
    {
        if(bool(i32(bool(i32((f32(sign(v_1654_loc_6_0)) * v_1652_loc_3_0 * v_1656_loc_9_0) < v_1657_loc_11_0)))))
        {
            v_1650_loc_13_0 = sqrt(v_1651_loc_8_0 + v_1656_loc_9_0) * v_1653_loc_4_0 - r1_1;
        }
        else
        {
            v_1650_loc_13_0 = (sqrt(v_1651_loc_8_0 * v_1652_loc_3_0 * v_1653_loc_4_0) + v_1654_loc_6_0 * v_1655_loc_2_0) * v_1653_loc_4_0 - r1_1;
        }
    }
    return v_1650_loc_13_0;
}

fn sdRoundCone_s1_0( p_11 : vec3<f32>,  r1_2 : f32,  r2_2 : f32,  h_7 : f32) -> f32
{
    var v_1780_loc_0_0 : vec2<f32> = vec2<f32>(length(p_11.xz), p_11.y);
    var v_1782_loc_1_0 : f32 = (r1_2 - r2_2) / h_7;
    var v_1781_loc_2_0 : f32 = sqrt(1.0f - v_1782_loc_1_0 * v_1782_loc_1_0);
    var v_1783_loc_3_0 : f32 = dot(v_1780_loc_0_0, vec2<f32>(- v_1782_loc_1_0, v_1781_loc_2_0));
    var v_1779_loc_5_0 : f32;
    if(bool(i32(bool(i32(v_1783_loc_3_0 < 0.0f)))))
    {
        v_1779_loc_5_0 = length(v_1780_loc_0_0) - r1_2;
    }
    else
    {
        if(bool(i32(bool(i32(v_1783_loc_3_0 > (v_1781_loc_2_0 * h_7))))))
        {
            v_1779_loc_5_0 = length(v_1780_loc_0_0 - vec2<f32>(0.0f, h_7)) - r2_2;
        }
        else
        {
            v_1779_loc_5_0 = dot(v_1780_loc_0_0, vec2<f32>(v_1781_loc_2_0, v_1782_loc_1_0)) - r1_2;
        }
    }
    return v_1779_loc_5_0;
}

fn map_0( pos_1 : vec3<f32>) -> vec2<f32>
{
    var _S50 : f32 = pos_1.y;
    var v_45_loc_0_0 : vec2<f32> = vec2<f32>(_S50, 0.0f);
    var v_45_loc_0_1 : vec2<f32>;
    if(bool(i32(bool(i32((sdBox_0(pos_1 - vec3<f32>(-2.0f, 0.30000001192092896f, 0.25f), vec3<f32>(0.30000001192092896f, 0.30000001192092896f, 1.0f))) < _S50)))))
    {
        v_45_loc_0_1 = opU_0(opU_0(v_45_loc_0_0, vec2<f32>(sdSphere_0(pos_1 - vec3<f32>(-2.0f, 0.25f, 0.0f), 0.25f), 26.89999961853027344f)), vec2<f32>(sdRhombus_0((pos_1 - vec3<f32>(-2.0f, 0.25f, 1.0f)).xzy, 0.15000000596046448f, 0.25f, 0.03999999910593033f, 0.07999999821186066f), 17.0f));
    }
    else
    {
        v_45_loc_0_1 = v_45_loc_0_0;
    }
    const _S51 : vec3<f32> = vec3<f32>(0.34999999403953552f, 0.30000001192092896f, 2.5f);
    if(bool(i32(bool(i32((sdBox_0(pos_1 - vec3<f32>(0.0f, 0.30000001192092896f, -1.0f), _S51)) < (v_45_loc_0_1.x))))))
    {
        v_45_loc_0_1 = opU_0(opU_0(opU_0(opU_0(opU_0(v_45_loc_0_1, vec2<f32>(sdCappedTorus_0((pos_1 - vec3<f32>(0.0f, 0.30000001192092896f, 1.0f)) * vec3<f32>(1.0f, -1.0f, 1.0f), vec2<f32>(0.86602497100830078f, -0.5f), 0.25f, 0.05000000074505806f), 25.0f)), vec2<f32>(sdBoxFrame_0(pos_1 - vec3<f32>(0.0f, 0.25f, 0.0f), vec3<f32>(0.30000001192092896f, 0.25f, 0.20000000298023224f), 0.02500000037252903f), 16.89999961853027344f)), vec2<f32>(sdCone_0(pos_1 - vec3<f32>(0.0f, 0.44999998807907104f, -1.0f), vec2<f32>(0.60000002384185791f, 0.80000001192092896f), 0.44999998807907104f), 55.0f)), vec2<f32>(sdCappedCone1_0(pos_1 - vec3<f32>(0.0f, 0.25f, -2.0f), 0.25f, 0.25f, 0.10000000149011612f), 13.67000007629394531f)), vec2<f32>(sdSolidAngle_0(pos_1 - vec3<f32>(0.0f, 0.0f, -3.0f), vec2<f32>(3.0f, 4.0f) / vec2<f32>(5.0f), 0.40000000596046448f), 49.13000106811523438f));
    }
    if(bool(i32(bool(i32((sdBox_0(pos_1 - vec3<f32>(1.0f, 0.30000001192092896f, -1.0f), _S51)) < (v_45_loc_0_1.x))))))
    {
        v_45_loc_0_1 = opU_0(opU_0(opU_0(opU_0(opU_0(v_45_loc_0_1, vec2<f32>(sdTorus_0((pos_1 - vec3<f32>(1.0f, 0.30000001192092896f, 1.0f)).xzy, vec2<f32>(0.25f, 0.05000000074505806f)), 7.09999990463256836f)), vec2<f32>(sdBox_0(pos_1 - vec3<f32>(1.0f, 0.25f, 0.0f), vec3<f32>(0.30000001192092896f, 0.25f, 0.10000000149011612f)), 3.0f)), vec2<f32>(sdCapsule_0(pos_1 - vec3<f32>(1.0f, 0.0f, -1.0f), vec3<f32>(-0.10000000149011612f, 0.10000000149011612f, -0.10000000149011612f), vec3<f32>(0.20000000298023224f, 0.40000000596046448f, 0.20000000298023224f), 0.10000000149011612f), 31.89999961853027344f)), vec2<f32>(sdCylinder1_0(pos_1 - vec3<f32>(1.0f, 0.25f, -2.0f), vec2<f32>(0.15000000596046448f, 0.25f)), 8.0f)), vec2<f32>(sdHexPrism_0(pos_1 - vec3<f32>(1.0f, 0.20000000298023224f, -3.0f), vec2<f32>(0.20000000298023224f, 0.05000000074505806f)), 18.39999961853027344f));
    }
    if(bool(i32(bool(i32((sdBox_0(pos_1 - vec3<f32>(-1.0f, 0.34999999403953552f, -1.0f), vec3<f32>(0.34999999403953552f, 0.34999999403953552f, 2.5f))) < (v_45_loc_0_1.x))))))
    {
        v_45_loc_0_1 = opU_0(opU_0(opU_0(opU_0(v_45_loc_0_1, vec2<f32>(sdPyramid_0(pos_1 - vec3<f32>(-1.0f, -0.60000002384185791f, -3.0f), 1.0f), 13.56000041961669922f)), vec2<f32>(sdOctahedron0_0(pos_1 - vec3<f32>(-1.0f, 0.15000000596046448f, -2.0f), 0.34999999403953552f), 23.55999946594238281f)), vec2<f32>(sdTriPrism_0(pos_1 - vec3<f32>(-1.0f, 0.15000000596046448f, -1.0f), vec2<f32>(0.30000001192092896f, 0.05000000074505806f)), 43.5f)), vec2<f32>(sdEllipsoid_0(pos_1 - vec3<f32>(-1.0f, 0.25f, 0.0f), vec3<f32>(0.20000000298023224f, 0.25f, 0.05000000074505806f)), 43.1699981689453125f));
    }
    if(bool(i32(bool(i32((sdBox_0(pos_1 - vec3<f32>(2.0f, 0.30000001192092896f, -1.0f), _S51)) < (v_45_loc_0_1.x))))))
    {
        const _S52 : vec3<f32> = vec3<f32>(0.10000000149011612f, 0.0f, 0.0f);
        v_45_loc_0_1 = opU_0(opU_0(opU_0(opU_0(opU_0(v_45_loc_0_1, vec2<f32>(sdOctogonPrism_0(pos_1 - vec3<f32>(2.0f, 0.20000000298023224f, -3.0f), 0.20000000298023224f, 0.05000000074505806f), 51.79999923706054688f)), vec2<f32>(sdCylinder2_0(pos_1 - vec3<f32>(2.0f, 0.14000000059604645f, -2.0f), vec3<f32>(0.10000000149011612f, -0.10000000149011612f, 0.0f), vec3<f32>(-0.20000000298023224f, 0.34999999403953552f, 0.10000000149011612f), 0.07999999821186066f), 31.20000076293945312f)), vec2<f32>(sdCappedCone2_0(pos_1 - vec3<f32>(2.0f, 0.09000000357627869f, -1.0f), _S52, vec3<f32>(-0.20000000298023224f, 0.40000000596046448f, 0.10000000149011612f), 0.15000000596046448f, 0.05000000074505806f), 46.09999847412109375f)), vec2<f32>(sdRoundCone_s2_0(pos_1 - vec3<f32>(2.0f, 0.15000000596046448f, 0.0f), _S52, vec3<f32>(-0.10000000149011612f, 0.34999999403953552f, 0.10000000149011612f), 0.15000000596046448f, 0.05000000074505806f), 51.70000076293945312f)), vec2<f32>(sdRoundCone_s1_0(pos_1 - vec3<f32>(2.0f, 0.20000000298023224f, 1.0f), 0.20000000298023224f, 0.10000000149011612f, 0.30000001192092896f), 37.0f));
    }
    return v_45_loc_0_1;
}

fn raycast_0( ro : vec3<f32>,  rd : vec3<f32>) -> vec2<f32>
{
    var res = vec2(-1.0,-1.0);

    var tmin: f32 = 1.0;
    var tmax: f32 = 20.0;

    // raytrace floor plane
    var tp1 : f32 = (0.0-ro.y)/rd.y;
    if( tp1>0.0 )
    {
        tmax = min( tmax, tp1 );
        res = vec2( tp1, 1.0 );
    }
    //else return res;

    // raymarch primitives
    let tb = iBox_0( ro-vec3(0.0,0.4,-0.5), rd, vec3(2.5,0.41,3.0) );
    if( tb.x<tb.y && tb.y>0.0 && tb.x<tmax)
    {
        //return vec2(tb.x,2.0);
        tmin = max(tb.x,tmin);
        tmax = min(tb.y,tmax);

        var t : f32 = tmin;
        for(var i=0; i<70 && t<tmax; i++ )
        {
            let h = map_0( ro+rd*t );
            if( abs(h.x)<(0.0001*t) )
            { 
                res = vec2(t,h.y); 
                break;
            }
            t += h.x;
        }
    }
    
    return res;
    // const _S53 : vec2<f32> = vec2<f32>(-1.0f, -1.0f);
    // var v_2571_loc_3_0 : f32 = (0.0f - ro_1.y) / rd_1.y;
    // var v_2565_loc_2_0 : f32;
    // var v_2558_loc_0_0 : vec2<f32>;
    // if(bool(i32(bool(i32(v_2571_loc_3_0 > 0.0f)))))
    // {
    //     var _S54 : vec2<f32> = vec2<f32>(v_2571_loc_3_0, 1.0f);
    //     v_2565_loc_2_0 = min(20.0f, v_2571_loc_3_0);
    //     v_2558_loc_0_0 = _S54;
    // }
    // else
    // {
    //     v_2565_loc_2_0 = 20.0f;
    //     v_2558_loc_0_0 = _S53;
    // }
    // var v_2566_loc_4_0 : vec2<f32> = iBox_0(ro_1 - vec3<f32>(0.0f, 0.40000000596046448f, -0.5f), rd_1, vec3<f32>(2.5f, 0.40999999642372131f, 3.0f));
    // var _S55 : f32 = v_2566_loc_4_0.x;
    // var _S56 : f32 = v_2566_loc_4_0.y;
    // var v_2568_0 : i32;
    // if(_S55 >= _S56)
    // {
    //     v_2568_0 = i32(0);
    // }
    // else
    // {
    //     if(_S56 <= 0.0f)
    //     {
    //         v_2568_0 = i32(0);
    //     }
    //     else
    //     {
    //         v_2568_0 = i32(_S55 < v_2565_loc_2_0);
    //     }
    // }
    // if(bool(i32(bool(i32(bool(v_2568_0))))))
    // {
    // }
    // return v_2558_loc_0_0;
}

fn calcNormal_0( pos_2 : vec3<f32>) -> vec3<f32>
{
    var v_2_loc_0_0 : vec2<f32> = vec2<f32>(1.0f, -1.0f) * vec2<f32>(0.57730001211166382f) * vec2<f32>(0.00050000002374873f);
    var _S57 : vec3<f32> = v_2_loc_0_0.xyy;
    var _S58 : vec3<f32> = v_2_loc_0_0.yyx;
    var _S59 : vec3<f32> = v_2_loc_0_0.yxy;
    var _S60 : vec3<f32> = v_2_loc_0_0.xxx;
    return normalize(_S57 * vec3<f32>(map_0(pos_2 + _S57).x) + _S58 * vec3<f32>(map_0(pos_2 + _S58).x) + _S59 * vec3<f32>(map_0(pos_2 + _S59).x) + _S60 * vec3<f32>(map_0(pos_2 + _S60).x));
}

fn checkersGradBox_0( p_12 : vec2<f32>,  dpdx_0 : vec2<f32>,  dpdy_0 : vec2<f32>) -> f32
{
    var v_1949_loc_0_0 : vec2<f32> = abs(dpdx_0) + abs(dpdy_0) + vec2<f32>(0.00100000004749745f);
    var _S61 : vec2<f32> = vec2<f32>(0.5f);
    var _S62 : vec2<f32> = _S61 * v_1949_loc_0_0;
    var v_1950_loc_1_0 : vec2<f32> = vec2<f32>(2.0f) * (abs(fract((p_12 - _S62) * _S61) - _S61) - abs(fract((p_12 + _S62) * _S61) - _S61)) / v_1949_loc_0_0;
    return 0.5f - 0.5f * v_1950_loc_1_0.x * v_1950_loc_1_0.y;
}

fn ZERO_0() -> i32
{
    return i32(0);
}

fn calcSoftshadow_0( ro_2 : vec3<f32>,  rd_2 : vec3<f32>,  mint_0 : f32,  maxt_0 : f32) -> f32
{
    var v_1857_loc_0_0 : f32 = (0.80000001192092896f - ro_2.y) / rd_2.y;
    var v_1852_loc_1_0 : f32;
    if(bool(i32(bool(i32(v_1857_loc_0_0 > 0.0f)))))
    {
        v_1852_loc_1_0 = min(maxt_0, v_1857_loc_0_0);
    }
    else
    {
        v_1852_loc_1_0 = maxt_0;
    }
    var v_1847_loc_5_0 : i32 = ZERO_0();
    var v_1851_loc_3_0 : f32 = mint_0;
    var v_1846_loc_2_0 : f32 = 1.0f;
    for(;;)
    {
        if(bool(i32(bool(i32(v_1847_loc_5_0 < i32(24))))))
        {
            var v_1854_loc_6_0 : f32 = map_0(ro_2 + rd_2 * vec3<f32>(v_1851_loc_3_0)).x;
            var v_1846_loc_2_1 : f32 = min(v_1846_loc_2_0, clamp(8.0f * v_1854_loc_6_0 / v_1851_loc_3_0, 0.0f, 1.0f));
            var v_1851_loc_3_1 : f32 = v_1851_loc_3_0 + clamp(v_1854_loc_6_0, 0.00999999977648258f, 0.20000000298023224f);
            var v_1848_0 : i32;
            if(v_1846_loc_2_1 < 0.00400000018998981f)
            {
                v_1848_0 = i32(1);
            }
            else
            {
                v_1848_0 = i32(v_1851_loc_3_1 > v_1852_loc_1_0);
            }
            if(bool(i32(bool(i32(bool(v_1848_0))))))
            {
            }
            else
            {
                v_1847_loc_5_0 = v_1847_loc_5_0 + i32(1);
                v_1851_loc_3_0 = v_1851_loc_3_1;
                v_1846_loc_2_0 = v_1846_loc_2_1;
                continue;
            }
            v_1846_loc_2_0 = v_1846_loc_2_1;
        }
        var v_1846_loc_2_2 : f32 = clamp(v_1846_loc_2_0, 0.0f, 1.0f);
        return v_1846_loc_2_2 * v_1846_loc_2_2 * (3.0f - 2.0f * v_1846_loc_2_2);
    }
}

fn SmoothStep_0( edge0_0 : f32,  edge1_0 : f32,  x_0 : f32) -> f32
{
    var v_2717_loc_0_0 : f32 = clamp((x_0 - edge0_0) / 0.0f, 0.0f, 1.0f);
    return v_2717_loc_0_0 * v_2717_loc_0_0 * (3.0f - 2.0f * v_2717_loc_0_0);
}

fn render_0( ro_3 : vec3<f32>,  rd_3 : vec3<f32>,  rdx_0 : vec3<f32>,  rdy_0 : vec3<f32>) -> vec3<f32>
{
    var _S63 : f32 = rd_3.y;
    var v_2242_loc_0_0 : f32 = max(_S63, 0.0f) * 0.30000001192092896f;
    const _S64 : vec3<f32> = vec3<f32>(0.69999998807907104f, 0.69999998807907104f, 0.89999997615814209f);
    var _S65 : vec3<f32> = _S64 - vec3<f32>(v_2242_loc_0_0, v_2242_loc_0_0, v_2242_loc_0_0);
    var v_2243_loc_2_0 : vec2<f32> = raycast_0(ro_3, rd_3);
    var v_2219_loc_3_0 : f32 = v_2243_loc_2_0.x;
    var v_2234_loc_4_0 : f32 = v_2243_loc_2_0.y;
    var v_2217_loc_1_0 : vec3<f32>;
    if(bool(i32(bool(i32(v_2234_loc_4_0 > -0.5f)))))
    {
        var v_2230_loc_6_0 : vec3<f32> = ro_3 + vec3<f32>(v_2219_loc_3_0) * rd_3;
        const _S66 : vec3<f32> = vec3<f32>(0.0f, 1.0f, 0.0f);
        var _S67 : i32 = i32(v_2234_loc_4_0 < 1.5f);
        var _S68 : bool = bool(i32(bool(_S67)));
        var v_2220_loc_7_0 : vec3<f32>;
        if(bool(i32(bool(i32(_S67 == i32(0))))))
        {
            v_2220_loc_7_0 = calcNormal_0(v_2230_loc_6_0);
        }
        else
        {
            v_2220_loc_7_0 = _S66;
        }
        var v_2222_loc_8_0 : vec3<f32> = reflect(rd_3, v_2220_loc_7_0);
        var v_2224_loc_9_0 : f32;
        if(_S68)
        {
            var _S69 : vec3<f32> = vec3<f32>(_S63);
            var _S70 : vec3<f32> = vec3<f32>(ro_3.y);
            var v_2236_loc_16_0 : vec3<f32> = _S70 * (rd_3 / _S69 - rdx_0 / vec3<f32>(rdx_0.y));
            var v_2237_loc_17_0 : vec3<f32> = _S70 * (rd_3 / _S69 - rdy_0 / vec3<f32>(rdy_0.y));
            var _S71 : vec2<f32> = vec2<f32>(3.0f);
            v_2217_loc_1_0 = vec3<f32>(0.15000000596046448f) + vec3<f32>(checkersGradBox_0(_S71 * vec2<f32>(v_2230_loc_6_0.x, v_2230_loc_6_0.z), _S71 * vec2<f32>(v_2236_loc_16_0.x, v_2236_loc_16_0.z), _S71 * vec2<f32>(v_2237_loc_17_0.x, v_2237_loc_17_0.z))) * vec3<f32>(0.05000000074505806f);
            v_2224_loc_9_0 = 0.40000000596046448f;
        }
        else
        {
            v_2217_loc_1_0 = vec3<f32>(0.30000001192092896f) + vec3<f32>(0.30000001192092896f) * sin(vec3<f32>((v_2234_loc_4_0 * 2.0f)) + vec3<f32>(0.0f, 1.0f, 2.0f));
            v_2224_loc_9_0 = 1.0f;
        }
        var v_2227_loc_21_0 : vec3<f32> = normalize(vec3<f32>(-0.5f, 0.40000000596046448f, -0.60000002384185791f));
        var v_2228_loc_22_0 : vec3<f32> = normalize(v_2227_loc_21_0 - rd_3);
        var v_2229_loc_23_0 : f32 = clamp(dot(v_2220_loc_7_0, v_2227_loc_21_0), 0.0f, 1.0f) * calcSoftshadow_0(v_2230_loc_6_0, v_2227_loc_21_0, 0.01999999955296516f, 2.5f);
        const _S72 : vec3<f32> = vec3<f32>(1.29999995231628418f, 1.0f, 0.69999998807907104f);
        var v_2221_loc_26_0 : f32 = sqrt(clamp(0.5f + 0.5f * v_2220_loc_7_0.y, 0.0f, 1.0f));
        v_2217_loc_1_0 = mix(v_2217_loc_1_0 * vec3<f32>(2.20000004768371582f) * vec3<f32>(v_2229_loc_23_0) * _S72 + vec3<f32>((5.0f * (pow(clamp(dot(v_2220_loc_7_0, v_2228_loc_22_0), 0.0f, 1.0f), 16.0f) * v_2229_loc_23_0 * (0.03999999910593033f + 0.95999997854232788f * pow(clamp(1.0f - dot(v_2228_loc_22_0, v_2227_loc_21_0), 0.0f, 1.0f), 5.0f))))) * _S72 * vec3<f32>(v_2224_loc_9_0) + v_2217_loc_1_0 * vec3<f32>(0.60000002384185791f) * vec3<f32>(v_2221_loc_26_0) * vec3<f32>(0.40000000596046448f, 0.60000002384185791f, 1.14999997615814209f) + vec3<f32>((2.0f * (SmoothStep_0(-0.20000000298023224f, 0.20000000298023224f, v_2222_loc_8_0.y) * v_2221_loc_26_0 * (0.03999999910593033f + 0.95999997854232788f * pow(clamp(1.0f + dot(v_2220_loc_7_0, rd_3), 0.0f, 1.0f), 5.0f))))) * vec3<f32>(0.40000000596046448f, 0.60000002384185791f, 1.29999995231628418f) * vec3<f32>(v_2224_loc_9_0), _S64, vec3<f32>((1.0f - exp(-0.00009999999747379f * v_2219_loc_3_0 * v_2219_loc_3_0 * v_2219_loc_3_0))));
    }
    else
    {
        v_2217_loc_1_0 = _S65;
    }
    return vec3<f32>(clamp(v_2217_loc_1_0.x, 0.0f, 1.0f), clamp(v_2217_loc_1_0.y, 0.0f, 1.0f), clamp(v_2217_loc_1_0.z, 0.0f, 1.0f));
}

struct pixelOutput_0
{
    @location(0) output_0 : vec4<f32>,
};

@fragment
fn fs(@builtin(position) vertexIn_0 : vec4<f32>) -> pixelOutput_0
{
    const v_1999_loc_0_0 : vec2<f32> = vec2<f32>(1920.0f, 1080.0f);
    var _S73 : vec2<f32> = v_1999_loc_0_0 - vertexIn_0.xy;
    const v_2016_loc_4_0 : vec3<f32> = vec3<f32>(0.25f, -0.75f, -0.75f);
    var _S74 : f32 = 0.10000000149011612f * (32.0f + globalParams_0.v_0_iTime_0 * 1.5f) + 7.0f;
    var v_2010_loc_5_0 : vec3<f32> = v_2016_loc_4_0 + vec3<f32>(4.5f * cos(_S74), 2.20000004768371582f, 4.5f * sin(_S74));
    var v_2004_loc_7_0 : vec3<f32> = normalize(v_2016_loc_4_0 - v_2010_loc_5_0);
    var v_2002_loc_9_0 : vec3<f32> = normalize(cross(v_2004_loc_7_0, vec3<f32>(sin(0.0f), cos(0.0f), 0.0f)));
    var _S75 : vec3<f32> = cross(v_2002_loc_9_0, v_2004_loc_7_0);
    var _S76 : vec3<f32> = vec3<f32>(0.0f);
    var v_1993_loc_12_0 : i32 = i32(0);
    var v_2012_loc_11_0 : vec3<f32> = _S76;
    for(;;)
    {
        if(bool(i32(bool(i32(v_1993_loc_12_0 < i32(1))))))
        {
            var _S77 : vec2<f32> = vec2<f32>(1080.0f);
            var v_2000_loc_16_0 : vec2<f32> = vec2<f32>(2.0f * _S73.x - 1920.0f, 2.0f * _S73.y - 1080.0f) / _S77;
            var _S78 : vec2<f32> = vec2<f32>(2.0f);
            var _S79 : vec3<f32> = v_2012_loc_11_0 + pow(render_0(v_2010_loc_5_0, MatrixMultiplyVector_0(v_2002_loc_9_0, _S75, v_2004_loc_7_0, normalize(vec3<f32>(v_2000_loc_16_0.x, v_2000_loc_16_0.y, 2.5f))), MatrixMultiplyVector_0(v_2002_loc_9_0, _S75, v_2004_loc_7_0, normalize(vec3<f32>((_S78 * (_S73 + vec2<f32>(1.0f, 0.0f)) - v_1999_loc_0_0) / _S77, 2.5f))), MatrixMultiplyVector_0(v_2002_loc_9_0, _S75, v_2004_loc_7_0, normalize(vec3<f32>((_S78 * (_S73 + vec2<f32>(0.0f, 1.0f)) - v_1999_loc_0_0) / _S77, 2.5f)))), vec3<f32>(0.4544999897480011f));
            v_1993_loc_12_0 = v_1993_loc_12_0 + i32(1);
            v_2012_loc_11_0 = _S79;
            continue;
        }
        var _S80 : pixelOutput_0 = pixelOutput_0( vec4<f32>(v_2012_loc_11_0, 1.0f) );
        return _S80;
    }
}

struct vertexOutput_0
{
    @builtin(position) output_1 : vec4<f32>,
};

struct vertexInput_0
{
    @location(0) position_0 : vec2<f32>,
};

@vertex
fn vs( _S81 : vertexInput_0) -> vertexOutput_0
{
    var _S82 : vertexOutput_0 = vertexOutput_0( vec4<f32>(_S81.position_0.xy, 0.0f, 1.0f) );
    return _S82;
}


`

interface InteractiveState {
  loop: boolean;
}

interface RealtimeState {
  color: {
    r: number;
    g: number;
    b: number;
  };
}

export async function BatchRenderMain() {
  const canvas = document.getElementById("render-root") as
    | HTMLCanvasElement
    | undefined;
  if (!canvas || !(canvas instanceof HTMLCanvasElement)) {
    throw Error("Failed to get canvas");
  }

  const ui = document.getElementById("ui-root") as HTMLDivElement | undefined;
  if (!ui || !(ui instanceof HTMLDivElement)) {
    throw Error("Failed to get ui root");
  }

  const format = navigator.gpu.getPreferredCanvasFormat();
  const adapter = await navigator.gpu.requestAdapter();
  const device = await adapter?.requestDevice();
  if (!device) {
    throw new Error(`Failed to request adapter`);
  }

  const context = canvas.getContext("webgpu");
  if (!context) {
    throw new Error(`Failed to get webgpu context`);
  }

  context.configure({
    device,
    format,
    alphaMode: "opaque",
  });

  let interactiveState: InteractiveState = {
    loop: true,
  };
  let needOneTimeRender = true;
  const realtimeState: RealtimeState = {
    color: { r: 0.1, g: 0.3, b: 0.3 },
  };

  createInteractiveUserInterface(ui, interactiveState, (s) => {
    interactiveState = s;
  });
  createRealtimeUserInterface(realtimeState);

  // const shaderName = "SampleFragmentShader";
  // const isUniformTest = true;
  // const shaderName = isUniformTest
  //   ? "SimpleUniformShader"
  //   : "SampleFragmentShader";
  //const code = await (await fetch(`/ilsl/wgsl/QuadShader`)).text();

  const shaderName = "RaymarchingPrimitiveShader";
  // const shaderName = "MandelbrotDistanceShader";
  const meshName = "ScreenQuad";
  // const vertexBufferLayout = await (
  //   await fetch(`/ilsl/wgsl/vertexbufferlayout/${shaderName}`)
  // ).json();
  // const vertexBufferLayout = JSON.parse(vertexBufferLayoutJson);
  const code = await (await fetch(`/ilsl/compile/${shaderName}/wgsl`)).text();
  const meshVertices = await (
    await fetch(`/api/Mesh/${meshName}/vertex`)
  ).arrayBuffer(); //Quad
  const meshIndices = await (
    await fetch(`/api/Mesh/${meshName}/index`)
  ).arrayBuffer(); //Quad
  // const bindGroupLayoutDescriptor = await (
  //   await fetch(`/ilsl/wgsl/bindgrouplayoutdescriptorbuffer/${shaderName}`)
  // ).json();

  const targetCode = `struct VertexInput {
  @location(0) position: vec2<f32>
};

@group(0) @binding(0) var<uniform> iResolution: vec2f;
@group(0) @binding(1) var<uniform> iTime: f32;


@vertex
fn vs(vert: VertexInput) -> @builtin(position) vec4<f32>
{
  return vec4<f32>(vert.position.x, vert.position.y, 0f, 1f);
}

fn ZERO() -> i32
{
    return 0;
}

fn sdBox(p: vec3f, b: vec3f) -> f32
{
    var d = abs(p) - b;
    return min(max(d.x, max(d.y, d.z)), 0.0) + length(vec3f(max(d.x, 0.0), max(d.y, 0.0), max(d.z, 0.0)));
}

fn sdSphere(p: vec3f, s: f32) -> f32
{
    return length(p) - s;
}

fn opU(d1: vec2f, d2: vec2f) -> vec2f
{
  if(d1.x < d2.x) 
  {
    return d1;
  }
  else
  {
    return d2;
  }
}

fn map(pos: vec3f ) -> vec2f
{
    var res = vec2f(pos.y, 0.0);

    // bounding box
    if( sdBox(pos - vec3f(-2.0, 0.3, 0.25), vec3f(0.3, 0.3, 1.0) ) < res.x)
    {
      res = opU(res, vec2(sdSphere(pos - vec3f(-2.0, 0.25, 0.0), 0.25 ), 26.9 ) );
    }
    return res;
}

fn iBox(ro: vec3f, rd: vec3f, rad: vec3f ) -> vec2f
{
    var m = 1.0/rd;
    var n = m*ro;
    var k = abs(m) * rad;
    var t1 = -n - k;
    var t2 = -n + k;
	  return vec2f( max( max( t1.x, t1.y ), t1.z ), min( min( t2.x, t2.y ), t2.z ) );
}

fn raycast(ro: vec3f, rd: vec3f) -> vec2f
{
    var res = vec2(-1.0,-1.0);

    var tmin = f32(1.0);
    var tmax = f32(20.0);

    // raytrace floor plane
    var tp1 = (0.0-ro.y)/rd.y;
    if( tp1>0.0 )
    {
        tmax = min( tmax, tp1 );
        res = vec2f( tp1, 1.0 );
    }
    //else return res;
    
    // raymarch primitives   
    var tb = iBox( ro-vec3f(0.0, 0.4, -0.5), rd, vec3f(2.5, 0.41, 3.0) );
    if( tb.x < tb.y && tb.y > 0.0 && tb.x < tmax)
    {
        //return vec2(tb.x,2.0);
        tmin = max(tb.x,tmin);
        tmax = min(tb.y,tmax);

        var t = tmin;
        for(var i=0; i< 70 && t < tmax; i = i + 1 )
        {
            var h = map( ro + rd * t );
            if( abs(h.x) < (0.0001 * t) )
            { 
                res = vec2f(t,h.y); 
                break;
            }
            t = t + h.x;
        }
    }
    return res;
}

fn checkersGradBox(p: vec2f, dpdx: vec2f, dpdy: vec2f) -> f32
{
    // filter kernel
    var w = abs(dpdx) + abs(dpdy) + 0.001;
    // analytical integral (box filter)
    var i = 2.0 * (abs(fract((p - 0.5 * w) * 0.5) - 0.5)-abs(fract((p + 0.5 * w) * 0.5) - 0.5)) / w;
    // xor pattern
    return 0.5 - 0.5 * i.x * i.y;                  
}

fn calcNormal(pos: vec3f) -> vec3f
{
    var e = vec2f(1.0, -1.0) * 0.5773 * 0.0005;
    return normalize(e.xyy * map(pos + e.xyy).x + e.yyx * map(pos + e.yyx).x + e.yxy*map( pos + e.yxy ).x + e.xxx*map( pos + e.xxx ).x);
}

fn calcSoftshadow(ro: vec3f, rd: vec3f, mint: f32, tmax: f32) -> f32
{
    // bounding volume
    var tp:f32 = (0.8-ro.y) / rd.y; 
    var tMax = tmax;
    if( tp > 0.0 ) 
    {
        tMax = min( tmax, tp );
    } 

    var res: f32 = 1.0;
    var t = mint;
    for( var i = ZERO(); i < 24; i++ )
    {
		    var h = map( ro + rd * t ).x;
        var s = clamp(8.0 * h / t, 0.0, 1.0);
        res = min(res, s);
        t += clamp(h, 0.01, 0.2);
        if(res < 0.004 || t > tMax) 
        {
           break;
        }
    }
    res = clamp( res, 0.0, 1.0 );
    return res * res * (3.0 - 2.0 * res);
}

fn render(ro: vec3f, rd: vec3f, rdx: vec3f, rdy: vec3f) -> vec3f
{ 
    // background
    var col = vec3f(0.7, 0.7, 0.9) - max(rd.y, 0.0) * 0.3;
    
    // raycast scene
    var res: vec2f = raycast(ro,rd);
    var t = res.x;
	  var m = res.y;
    if( m > -0.5 )
    {
        var pos = ro + t*rd;
        var nor: vec3f;
        if (m<1.5)
        {
            nor = vec3f(0.0,1.0,0.0);
        }
        else
        {
            nor = calcNormal( pos );
        }
        var reflection = reflect( rd, nor );
        
        // material        
        col = 0.2 + 0.2 * sin( m * 2.0 + vec3(0.0, 1.0, 2.0) );
        var ks = f32(1.0);
        
        if( m < 1.5 )
        {
            // project pixel footprint into the plane
            var dpdx = ro.y * (rd / rd.y - rdx / rdx.y);
            var dpdy = ro.y * (rd / rd.y - rdy / rdy.y);

            var f = checkersGradBox( 3.0 * pos.xz, 3.0 * dpdx.xz, 3.0 * dpdy.xz );
            col = 0.15 + f * vec3f(0.05);
            ks = 0.4;
        }

        // lighting
        
		    var lin = vec3f(0.0);
        
        // sun
        {
            var lig = normalize( vec3f(-0.5, 0.4, -0.6) );
            var hal = normalize( lig - rd );
            var dif = clamp( dot( nor, lig ), 0.0, 1.0 );

        	  dif *= calcSoftshadow( pos, lig, 0.02, 2.5 );
			      var spe = pow( clamp( dot( nor, hal ), 0.0, 1.0 ),16.0);
            spe *= dif;
            spe *= 0.04 + 0.96 * pow(clamp(1.0 - dot(hal, lig), 0.0, 1.0), 5.0);

            lin += col * 2.20 * dif * vec3f(1.30, 1.00, 0.70);
            lin += 5.0 * spe * vec3f(1.30, 1.00, 0.70) * ks;
        }
        // sky
        {
            var dif = sqrt(clamp( 0.5+0.5 * nor.y, 0.0, 1.0 ));
            var spe = smoothstep( -0.2, 0.2, reflection.y );
            spe *= dif;
            spe *= 0.04 + 0.96 * pow(clamp(1.0 + dot(nor, rd), 0.0, 1.0), 5.0 );
            lin += col*0.60*dif*vec3f(0.40,0.60,1.15);
            lin += 2.00 * spe * vec3f(0.40,0.60,1.30) * ks;
        }
		    col = lin;
        col = mix( col, vec3(0.7,0.7,0.9), 1.0-exp( -0.0001*t*t*t ) );
    }

	  return vec3f(clamp(col.x, 0.0, 1.0), clamp(col.y, 0.0, 1.0), clamp(col.z, 0.0, 1.0));
}


fn setCamera(ro: vec3f, ta: vec3f, cr: f32) -> mat3x3f
{
	var cw = normalize(ta-ro);
	var cp = vec3f(sin(cr), cos(cr),0.0);
	var cu = normalize( cross(cw,cp) );
	var cv = ( cross(cu,cw) );
  return mat3x3f(cu, cv, cw );
}

@fragment
fn fs(@builtin(position) vertexIn: vec4<f32>) -> @location(0) vec4<f32>
{
  var time = 32.0 + iTime * 1.5;
  var fragCoord = vec2f(iResolution.x- vertexIn.x, iResolution.y - vertexIn.y);
  // camera	
  var ta = vec3( 0.25, -0.75, -0.75 );
  var ro = ta + vec3( 4.5*cos(0.1*iTime + 7.0), 2.2, 4.5*sin(0.1*iTime + 7.0) );
  //cameraToworld
  var ca = setCamera(ro, ta, 0.0);

  var tot = vec3f(0.0, 0.0, 0.0);

  var p = vec2f(2.0 * fragCoord.x - iResolution.x, 2.0 * fragCoord.y - iResolution.y) / iResolution.y;
  // focal length
  var fl = f32(2.5);

  // ray direction
  var rd = ca * normalize( vec3(p,fl) );

  // ray differentials
  var px = (2.0 * (fragCoord + vec2f(1.0, 0.0)) - iResolution.xy) / iResolution.y;
  var py = (2.0 * (fragCoord + vec2f(0.0, 1.0))- iResolution.xy) / iResolution.y;
  var rdx = ca * normalize( vec3f(px,fl) );
  var rdy = ca * normalize( vec3f(py,fl) );

  var col = render( ro, rd, rdx, rdy );

  // gamma correction
  col = pow(col, vec3f(0.4545));

  tot = tot + col;

  return vec4f(tot, 1.0);
}
  `;
  // Uniform Buffer to pass resolution
  const resolutionBufferSize = 16; // used 8
  const resolutionBuffer = device.createBuffer({
    size: resolutionBufferSize,
    usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
  });
  const resolution = new Float32Array([1920, 1080]);
  device.queue.writeBuffer(resolutionBuffer, 0, resolution);

  // Uniform Buffer to pass time
  const timeBufferSize = 16; // used 4
  const timeBuffer = device.createBuffer({
    size: timeBufferSize,
    usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
  });
  const time = new Float32Array([0, 0, 0, 0]);
  device.queue.writeBuffer(timeBuffer, 0, time);

  // // Vertex Buffer
  const verticesBufferSize = meshVertices.byteLength;
  const vertexBuffer = device.createBuffer({
    size: verticesBufferSize,
    usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
  });
  const vertices = new Float32Array(meshVertices);
  device.queue.writeBuffer(vertexBuffer, 0, vertices);

  // Index Buffer
  const indexBufferSize = meshIndices.byteLength;
  const indexBuffer = device.createBuffer({
    size: indexBufferSize,
    usage: GPUBufferUsage.INDEX | GPUBufferUsage.COPY_DST,
  });
  const indices = new Uint16Array(meshIndices);
  device.queue.writeBuffer(indexBuffer, 0, indices);

  // const minimumTriangleCode = await (await fetch("/ilsl/compile")).text();

  const module = device.createShaderModule({
    label: shaderName,
    // code,
    // code: minimumTriangleCode,
    // code: code,
    code : debugCode
    //     code: `
    // @vertex
    // fn vs (@builtin (vertex_index) vertexIndex:u32) -> @builtin(position) vec4<f32>
    // {
    // 	var x:f32 = f32(i32((1 - vertexIndex))) * 0.5f;
    // 	var y:f32 = f32(i32(((vertexIndex & 1) * 2 - 1))) * 0.5f;
    // 	return vec4<f32> (x, y, 0f, 1f);
    // }

    // @fragment
    // fn fs () ->@location (0)
    //  vec4<f32>
    // {
    // 	return vec4<f32> (1f, 1f, 0.5f, 1f);
    // }
    // `
    // code: `
    //   @vertex fn vs(
    //     @builtin(vertex_index) vertex_index : u32
    //   ) -> @builtin(position) vec4f {
    //     let x = f32(1 - i32(vertex_index)) * 0.5;
    //     let y = f32(i32(vertex_index & 1u) * 2 - 1) * 0.5;
    //     return vec4f(x, y, 0.0, 1.0);
    //   }

    //   @fragment fn fs() -> @location(0) vec4f {
    //     return vec4f(1.0, 1.0, 0.5, 1.0);
    //   }
    // `,
  });

  const bindGroupLayout = device.createBindGroupLayout(
    {
      entries: [
        {
          binding: 0,
          visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
          buffer: {
            type: "uniform",
          },
        },
        // {
        //   binding: 1,
        //   visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
        //   buffer: {
        //     type: "uniform",
        //   },
        // },
      ],
    }
    // bindGroupLayoutDescriptor
  );
  const bindGroup = device.createBindGroup({
    layout: bindGroupLayout,
    entries: [{ binding: 0, resource: { buffer: timeBuffer } }],
    // entries: [
    //   { binding: 0, resource: { buffer: resolutionBuffer } },
    //   { binding: 1, resource: { buffer: timeBuffer } },
    // ],
  });

  const pipelineLayout = device.createPipelineLayout({
    bindGroupLayouts: [
      bindGroupLayout, // @group(0)
    ],
  });

  const pipeline = device.createRenderPipeline({
    layout: pipelineLayout,
    // layout: "auto",
    vertex: {
      module: module,
      entryPoint: "vs",
      buffers: [
        {
          arrayStride: 2 * 4,
          stepMode: "vertex",
          attributes: [
            {
              shaderLocation: 0,
              offset: 0,
              format: "float32x2",
            },
          ],
        },
      ],
    },
    fragment: {
      module,
      targets: [
        {
          format,
        },
      ],
    },
    primitive: {
      topology: "triangle-list",
    },
  });

  const render = (f: number) => {
    if (!interactiveState.loop && !needOneTimeRender) {
      console.log("skip rendering");
      requestAnimationFrame(render);
      return;
    }
    needOneTimeRender = false;
    const aspect = canvas.width / canvas.height;

    const view = context.getCurrentTexture().createView();

    const encoder = device.createCommandEncoder();

    clearCanvas(encoder, view, realtimeState.color);

    const renderPassDescriptor: GPURenderPassDescriptor = {
      label: "our basic canvas renderPass",
      colorAttachments: [
        {
          view,
          loadOp: "load",
          storeOp: "store",
        },
      ],
    };
    var seconds = performance.now() / 1000;
    time.set([seconds, seconds]);
    device.queue.writeBuffer(timeBuffer, 0, time);
    const pass = encoder.beginRenderPass(renderPassDescriptor);
    pass.setPipeline(pipeline);
    pass.setBindGroup(0, bindGroup!);
    pass.setVertexBuffer(0, vertexBuffer);
    pass.setIndexBuffer(indexBuffer, "uint16");
    pass.drawIndexed(indices.length);
    // pass.draw(3);
    pass.end();

    device.queue.submit([encoder.finish()]);

    requestAnimationFrame(render);
  };

  requestAnimationFrame(render);
}

function createInteractiveUserInterface(
  uiRoot: HTMLDivElement,
  initState: InteractiveState,
  update: (s: InteractiveState) => void
) {
  const root = createRoot(uiRoot);
  root.render(
    createElement(InteractiveApp, {
      state: { loop: initState.loop },
      update,
    })
  );
}

function createRealtimeUserInterface(state: RealtimeState) {
  const gui = new GUI();
  gui.addColor(state, "color");
  gui.onChange(({ object }) => {
    console.log(object);
  });
}

function clearCanvas(
  encoder: GPUCommandEncoder,
  target: GPUTextureView,
  color: { r: number; g: number; b: number }
) {
  const pass = encoder.beginRenderPass({
    label: "clear-pass",
    colorAttachments: [
      {
        view: target,
        clearValue: [color.r, color.g, color.b, 1],
        loadOp: "clear",
        storeOp: "store",
      },
    ],
  });
  pass.end();
}
