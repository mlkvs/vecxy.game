#type vertex
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
uniform mat4 uModel;
uniform mat4 uTransform;
uniform float uTime;
uniform float uSplash;
out vec2 vUv;
out vec3 vWorld;
out float vAge;

void main()
{
    vec3 p = aPosition;
    vec3 origin = (uModel * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
    float seed = aNormal.x;
    vAge = fract(seed + uTime * (0.8 + fract(seed * 31.0) * 0.6));
    if (uSplash < 0.5)
    {
        float fall = fract(seed - uTime * (0.85 + fract(seed * 19.0) * 0.6));
        float tail = 0.14 + fract(seed * 43.0) * 0.16;
        p.y = -0.12 + fall * 8.0 + (aTexCoord.y - 0.5) * tail - origin.y;
        // World-space falling streaks with a slight wind.
        p.z += (1.0 - fall) * 0.55 + (aTexCoord.y - 0.5) * tail * 0.07;
    }
    else
    {
        float diameter = 0.04 + vAge * 0.28;
        p.xz += (aTexCoord - 0.5) * (diameter - 0.32);
    }
    vUv = aTexCoord;
    vWorld = (uModel * vec4(p, 1.0)).xyz;
    gl_Position = uTransform * vec4(p, 1.0);
}

#type fragment
#version 330 core
in vec2 vUv;
in vec3 vWorld;
in float vAge;
uniform float uSplash;
uniform vec3 uCameraPosition;
out vec4 FragColor;

void main()
{
    float alpha;
    if (uSplash < 0.5)
    {
        float edge = 1.0 - abs(vUv.x * 2.0 - 1.0);
        float tail = smoothstep(0.0, 0.2, vUv.y) * (1.0 - vUv.y * 0.6);
        alpha = edge * tail * 0.38;
    }
    else
    {
        float radius = length(vUv - 0.5);
        float ring = 1.0 - smoothstep(0.018, 0.065, abs(radius - 0.36));
        alpha = ring * (1.0 - vAge) * 0.38;
    }
    float fog = exp(-length(uCameraPosition - vWorld) * 0.035);
    vec3 color = vec3(0.72, 0.82, 0.92);
    FragColor = vec4(color, alpha * fog);
}
