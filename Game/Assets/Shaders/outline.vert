#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 4) in mat4 aModel;

uniform mat4 uViewProjection;

const float outlineWidth = 0.03;

void main()
{
    vec3 expandedPosition = aPosition + normalize(aNormal) * outlineWidth;
    gl_Position = uViewProjection * aModel * vec4(expandedPosition, 1.0);
}
