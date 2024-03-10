#version 300 es

precision mediump float;

layout(location = 0) in vec3 a_Position;
layout(location = 1) in vec2 a_TexCoord;
layout(location = 2) in vec3 a_Normal;

uniform mat4 u_Projection;
uniform mat4 u_View;
uniform mat4 u_Model;

out vec2 v_TexCoord;

void main() {
    gl_Position = u_Projection * u_View * u_Model * vec4(a_Position, 1.0f);
    v_TexCoord = a_TexCoord;
    v_TexCoord.y = 1.0 - v_TexCoord.y;
}
