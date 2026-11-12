#type vertex
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
uniform mat4 uModel;
uniform mat4 uTransform;
out vec3 vWorld;
out vec3 vNormal;
void main()
{
    vWorld = (uModel * vec4(aPosition, 1.0)).xyz;
    vNormal = mat3(uModel) * aNormal;
    gl_Position = uTransform * vec4(aPosition, 1.0);
}

#type fragment
#version 330 core
in vec3 vWorld;
in vec3 vNormal;
uniform vec3 uCameraPosition;
uniform float uTime;
out vec4 FragColor;
float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }
void main()
{
    // Slow individual beads slide down glass while the street remains visible.
    float horizontal = (vWorld.x + vWorld.z) * 23.0;
    float column = floor(horizontal);
    float seed = hash(column);
    float x = fract(horizontal) - 0.5;
    float vertical = vWorld.y * 2.7 + uTime * (0.17 + seed * 0.22) + seed * 13.0;
    float y = fract(vertical) - 0.5;
    float bead = 1.0 - smoothstep(0.09, 0.2, length(vec2(x * 2.8, y * 1.2)));
    float trail = (1.0 - smoothstep(0.013, 0.042, abs(x))) * smoothstep(-0.5, -0.15, y) * 0.32;
    float drops = max(bead, trail) * step(0.23, seed);
    vec3 viewDir = normalize(uCameraPosition - vWorld);
    float grazing = pow(1.0 - abs(dot(normalize(vNormal), viewDir)), 3.0);
    float alpha = 0.018 + grazing * 0.07 + drops * 0.2;
    vec3 color = vec3(0.19, 0.28, 0.34) + drops * vec3(0.2, 0.23, 0.26);
    FragColor = vec4(color, alpha);
}
