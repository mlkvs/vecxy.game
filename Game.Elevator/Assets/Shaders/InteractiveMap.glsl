#type vertex

#version 330 core

layout(location = 0) in vec2 aPosition;
layout(location = 1) in vec2 aTexCoord;

uniform mat4 uTransform;

out vec2 vTexCoord;

void main()
{
    vTexCoord = aTexCoord;
    gl_Position = uTransform * vec4(aPosition, 0.0, 1.0);
}

#type fragment

#version 330 core

in vec2 vTexCoord;

uniform sampler2D uMapTexture;
uniform sampler2D uOutlineTexture;
uniform sampler2D uMaskTexture;
uniform vec4 uActiveMaskColor;
uniform vec4 uMaskTexelSize;
uniform float uMaskTolerance;
uniform float uOutlineMaskRadius;

out vec4 oColor;

bool matchesActiveRegion(vec4 maskColor)
{
    return uActiveMaskColor.a > 0.5 &&
           maskColor.a >= 0.5 &&
           all(lessThanEqual(
               abs(maskColor.rgb - uActiveMaskColor.rgb),
               vec3(uMaskTolerance)));
}

bool activeRegionNear(vec2 uv)
{
    for (int y = -4; y <= 4; ++y)
    {
        if (abs(float(y)) > uOutlineMaskRadius)
            continue;

        for (int x = -4; x <= 4; ++x)
        {
            if (abs(float(x)) > uOutlineMaskRadius)
                continue;

            vec2 offset = vec2(float(x), float(y)) * uMaskTexelSize.xy;
            if (matchesActiveRegion(texture(uMaskTexture, uv + offset)))
                return true;
        }
    }

    return false;
}

void main()
{
    vec2 uv = vec2(vTexCoord.x, 1.0 - vTexCoord.y);
    vec4 mapColor = texture(uMapTexture, uv);
    vec4 outlineColor = texture(uOutlineTexture, uv);

    if (outlineColor.a <= 0.0 || !activeRegionNear(uv))
    {
        oColor = mapColor;
        return;
    }

    oColor = vec4(
        mix(mapColor.rgb, outlineColor.rgb, outlineColor.a),
        mapColor.a + outlineColor.a * (1.0 - mapColor.a));
}
