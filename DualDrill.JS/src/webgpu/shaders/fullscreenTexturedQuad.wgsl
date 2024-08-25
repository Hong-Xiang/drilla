@group(0) @binding(0) var mySampler : sampler;
@group(0) @binding(1) var myTexture : texture_3d<f32>;
@group(0) @binding(2) var<uniform> depthValue : vec4f;

struct VertexOutput {
  @builtin(position) Position : vec4f,
  @location(0) fragUV : vec2f,
}

const scale = sqrt(3);
const steps = 256;
const pos = array(
    vec2( 1.0,  1.0),
    vec2( 1.0, -1.0),
    vec2(-1.0, -1.0),
    vec2( 1.0,  1.0),
    vec2(-1.0, -1.0),
    vec2(-1.0,  1.0),
);

@vertex
fn vert_main(@builtin(vertex_index) VertexIndex : u32) -> VertexOutput {
 

  const uv = array(
    vec2(1.0, 0.0),
    vec2(1.0, 1.0),
    vec2(0.0, 1.0),
    vec2(1.0, 0.0),
    vec2(0.0, 1.0),
    vec2(0.0, 0.0),
  );

  var output : VertexOutput;
  output.Position = vec4(pos[VertexIndex], 0.0, 1.0);
  output.fragUV = vec2(pos[VertexIndex].x, - pos[VertexIndex].y) / 2 * scale;
  return output;
}

@fragment
fn frag_main(@location(0) fragUV : vec2f) -> @location(0) vec4f {
  let theta = depthValue.x;
  let phi = depthValue.y;
  let ru = vec3(
    sin(theta) * cos(phi),
    sin(theta) * sin(phi),
    cos(theta)
  );
  let thetau = vec3(
    cos(theta) * cos(phi),
    cos(theta) * sin(phi),
    -sin(theta)
  );
  let phiu = vec3(
    -sin(phi),
    cos(phi),
    0.0
  );
  let p = fragUV.x * thetau +  fragUV.y * phiu + depthValue.z * ru;
  // let p = vec3(fragUV.x, fragUV.y, depthValue.z) * vec3(1.0f, 1.0f, 1.0f);
  var value = 0.0f;
  var window = depthValue.w;
  for(var i = 0; i < steps; i++){
    let uv = (p + scale / 2.0f) / scale + ru * window * (1.0f / steps) * f32(i - steps / 2);
    value +=  textureSample(myTexture, mySampler, uv).r;
  }
  value = value / steps;

  // let value = (p + sqrt(3)) / 2;
  // return vec4(value, 1.0);
  // return vec4(fragUV, 0.0, 1.0);
  // return vec4(0.5, 1, 0.5, 1);
  // return vec4(uv.x, uv.x, uv.x, 1.0f);
  return vec4(vec3(value), 1.0f);
}
