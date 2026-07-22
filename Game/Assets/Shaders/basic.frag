#version 330 core

in vec3 vWorldNormal;
in vec4 vColor;
out vec4 fragColor;

void main()
{
    vec3 normal = normalize(vWorldNormal);
    vec3 lightDirection = normalize(vec3(0.4, 1.0, 0.3));
    float diffuse = max(dot(normal, lightDirection), 0.0);
    float lighting = 0.22 + diffuse * 0.78;
    fragColor = vec4(vColor.rgb * lighting, vColor.a);
}
