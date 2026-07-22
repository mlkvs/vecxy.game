#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec4 aColor;
layout(location = 3) in vec2 aTexCoord;
layout(location = 4) in mat4 aModel;

uniform mat4 uViewProjection;

out vec3 vWorldNormal;
out vec4 vColor;

void main()
{
    vec4 worldPosition = aModel * vec4(aPosition, 1.0);
    gl_Position = uViewProjection * worldPosition;
    vWorldNormal = normalize(mat3(transpose(inverse(aModel))) * aNormal);
    vColor = aColor;
}
