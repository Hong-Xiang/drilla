#version 300 es

uniform mat4 u_worldViewProjection;
uniform mat4 u_worldInverseTranspose;

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 normal;
layout(location = 2) in vec2 texcoord;

out vec2 v_texCoord;
out vec3 v_normal;

void main() {
    gl_Position = u_worldViewProjection * vec4(position, 1.0f);
    v_texCoord = texcoord;
    v_normal = (u_worldInverseTranspose * vec4(normal, 0)).xyz;
}