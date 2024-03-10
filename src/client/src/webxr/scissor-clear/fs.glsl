#version 300 es
precision mediump float;

out vec4 o_Color;
in vec2 v_TexCoord;

uniform vec4 u_Color;
uniform sampler2D u_TexID[16];

vec4 shader() {
    return u_Color;
}

void main() {
    o_Color = shader();
}