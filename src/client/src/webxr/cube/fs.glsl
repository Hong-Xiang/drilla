#version 300 es
precision highp float;

in vec2 v_texCoord;
in vec3 v_normal;

uniform vec3 u_lightDirection;
uniform sampler2D diffuseColor;

out vec4 outColor;

vec4 lit(float l, float h, float m) {
    return vec4(1.0f, max(l, 0.0f), (l > 0.0f) ? pow(max(0.0f, h), m) : 0.0f, 1.0f);
}

void main() {
    vec4 diffuseColor = texture(diffuseColor, v_texCoord);
    vec3 a_normal = normalize(v_normal);
    float l = dot(a_normal, u_lightDirection) * 0.5f + 0.5f;
    outColor = vec4(diffuseColor.rgb * l, diffuseColor.a);
    // outColor = vec4(v_normal, 1.0f);
    outColor = diffuseColor;
}