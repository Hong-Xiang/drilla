        @group(0) @binding(0) var mySampler : sampler;
        @group(0) @binding(1) var myTexture : texture_3d<f32>;
        @group(0) @binding(2) var<uniform> depthValue : vec4f;

        struct VertexOutput {
          @builtin(position) Position : vec4f,
          @location(0) fragUV : vec2f,
        }

        const steps = 256;
       

        @vertex
        fn vert_main(@builtin(vertex_index) VertexIndex : u32) -> VertexOutput {
          let scale = sqrt(3.0);
          var p : vec2f;
          if(vertex_index == 0u){ 
            p = vec2(1.0,  1.0);
          }
          if(vertex_index == 1u){ 
            p = vec2( 1.0, -1.0);
          }
          if(vertex_index == 2u){ 
            p = vec2(-1.0, -1.0);
          }
          if(vertex_index == 3u){ 
            p = vec2( 1.0,  1.0);
          }
          if(vertex_index == 4u){ 
            p = vec2(-1.0, -1.0);
          }
          if(vertex_index == 5u){ 
            p = vec2(-1.0,  1.0);
          }

          let pos = array(
            vec2( 1.0,  1.0),
            vec2( 1.0, -1.0),
            vec2(-1.0, -1.0),
            vec2( 1.0,  1.0),
            vec2(-1.0, -1.0),
            vec2(-1.0,  1.0),
          );

          var output : VertexOutput;
          output.Position = vec4(pos[VertexIndex], 0.0, 1.0);
          output.fragUV = vec2(pos[VertexIndex].x, - pos[VertexIndex].y) / 2 * scale;
          return output;
        }

        @fragment
        fn frag_main(@location(0) fragUV : vec2f) -> @location(0) vec4f {
          let scale = sqrt(3.0);
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
          var value = 0.0f;
          var window = depthValue.w;
          for(var i = 0; i < steps; i++){
            let uv = (p + scale / 2.0f) / scale + ru * window * (1.0f / steps) * f32(i - steps / 2);
            value +=  textureSample(myTexture, mySampler, uv).r;
          }
          value = value / steps;

          return vec4(vec3(value), 1.0f);
        }