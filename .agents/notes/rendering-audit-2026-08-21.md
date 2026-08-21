# Rendering audit — 2026-08-21

## Current frame path

```text
SceneInstance
  -> collect Camera, MeshRenderer, SpriteRenderer and forward lights
  -> SceneRenderTarget (RGBA8 + Depth24)
  -> skybox forward draw
  -> sprite forward draw
  -> opaque/transparent mesh forward draws with Lit.glsl
     -> direct + approximate ambient/environment lighting
     -> exposure + exponential tonemap + gamma encode inside Lit.glsl
  -> optional full-resolution post-process ping-pong
  -> copy to backbuffer or expose texture to editor viewport
```

The renderer is Forward, not Deferred or Forward+. There is no GBuffer and no
separate lighting pass.

## Baseline findings

- Scene and post-process render targets are RGBA8, not HDR.
- Tonemapping occurs inside the lit material shader before post-processing.
- No shadow pass, shadow maps, AO, SSR, reflection probes, TAA or motion vectors.
- Ambient/environment specular is an inexpensive sky/ground color approximation,
  not image-based lighting.
- Base color is decoded to linear in the shader with `pow(2.2)`. Normal and
  metallic/roughness textures remain linear, but the texture resource has no
  explicit semantic/color-space metadata.
- The normal matrix is inverse-transpose and therefore handles non-uniform scale.
- Tangent space is reconstructed from screen-space derivatives; imported tangents
  and normal texture strength are not supported.
- Material textures previously defaulted to nearest filtering and had no mipmaps.
- Direct BRDF previously used GGX NDF but only the red Fresnel channel, an
  incomplete geometry term, and no energy-conserving diffuse/specular split.
- Rendering statistics expose CPU-smoothed frame time and draw counts only. GPU
  timestamps/pass timings are not implemented, so honest GPU Before/After numbers
  are not available yet.

## First minimal engine improvement

- Material textures now default to linear filtering.
- Texture upload creates mip chains; linear minification uses trilinear filtering.
- Direct lights now use Cook-Torrance with GGX/Trowbridge-Reitz NDF, Smith geometry,
  Schlick RGB Fresnel and an energy-conserving Lambert diffuse lobe.
- Ambient diffuse and direct lighting are separated in the shader.

## Recommended next stage

Introduce an RGBA16F scene target and a mandatory final output pass that performs
exposure and ACES-like tonemapping. Preserve editor viewport behavior and make
existing post effects explicitly HDR-safe before adding bloom or auto exposure.
After that, add GPU timestamp queries before attempting shadows or AO.
