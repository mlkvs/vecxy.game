# Vecxy.Animations

`Vecxy.Animations` is the code-first skeletal animation runtime. Animation clips,
skins, joints, and vertex weights come from a self-contained GLB file. Animator
controllers are regular C# objects and do not require an editor.

## Supported GLB data

- one embedded binary buffer;
- node TRS animation channels;
- `STEP`, `LINEAR`, and `CUBICSPLINE` interpolation;
- skins with optional inverse bind matrices;
- four influences from `JOINTS_0` and `WEIGHTS_0`;
- up to 64 joints in one skin;
- multiple named clips in one model.

Morph-target weight animation and animation retargeting between different
skeletons are not supported. Clip overrides must target the same node layout.

## Creating a controller

Parameters are typed handles. Keep them next to the controller so gameplay code
can reuse the same instances.

```csharp
public static readonly AnimatorFloat Speed = new("Speed");
public static readonly AnimatorBool Grounded = new("Grounded", true);
public static readonly AnimatorInt Weapon = new("Weapon");
public static readonly AnimatorTrigger Jump = new("Jump");

var controller = AnimatorController.Build("Character", graph =>
{
    graph.Parameter(Speed)
        .Parameter(Grounded)
        .Parameter(Weapon)
        .Parameter(Jump);

    graph.Layer("Base", layer =>
    {
        layer.DefaultState("Locomotion", new BlendTree1D(Speed)
            .At(0.0f, model.GetAnimation("Idle"))
            .At(2.0f, model.GetAnimation("Walk"))
            .At(6.0f, model.GetAnimation("Run")));

        layer.State("Jump", model.GetAnimation("Jump"), loop: false);
        layer.AnyState().To("Jump").When(Jump).Duration(0.08f);
        layer.From("Jump").To("Locomotion")
            .When(Grounded, true)
            .AfterNormalizedTime(0.8f)
            .Duration(0.12f);
    });
});
```

`Build` validates duplicate names, unknown states and parameters, empty blend
trees, and invalid motion durations.

## Instantiating and driving a model

```csharp
using var asset = assets.Load<ModelAsset>("Models/Character.glb");
var model = new Model(asset);
var character = animations.Instantiate(scene, model, controller, "Player");

character.Animator.SetFloat(Speed, horizontalVelocity.Length());
character.Animator.SetBool(Grounded, isGrounded);
character.Animator.SetInt(Weapon, (int)weaponKind);

if (jumpStarted)
    character.Animator.SetTrigger(Jump);
```

Keep `Model` and its `AssetRef<ModelAsset>` alive until the scene objects using
them have been destroyed. `AnimatedModelInstance.GetBone(name)` can be used to
attach weapons to imported nodes.

## Transitions and direct playback

A transition can combine multiple conditions, normalized exit time, duration,
priority, and interruption behavior. Trigger parameters are consumed when a
transition starts.

```csharp
animator.Play("Idle");
animator.CrossFade("Run", 0.2f);
animator.ResetTrigger(Jump);

var state = animator.GetCurrentState("Base");
var normalizedTime = animator.GetNormalizedTime("Base");
var blending = animator.IsInTransition("Base");
```

## Layers, masks, and additive animation

The first layer normally controls the whole skeleton. Later layers can override
or add deltas to selected node hierarchies.

```csharp
graph.Layer("UpperBody", layer =>
{
    layer.Mask(BoneMask.From("Spine"));
    layer.DefaultState("Aim", model.GetAnimation("RifleAim"));
    layer.State("Shoot", model.GetAnimation("RifleShoot"), loop: false)
        .Event(0.12f, "FireProjectile");
});

graph.Layer("Recoil", layer =>
{
    layer.Mask(BoneMask.From("Spine"));
    layer.Additive();
    layer.Weight(0.75f);
    layer.DefaultState("Recoil", model.GetAnimation("RifleRecoil"));
});
```

Layer weight can also change at runtime with `SetLayerWeight`.

## Events

Events are markers on states because core glTF has no gameplay-event format.

```csharp
animator.EventFired += value =>
{
    if (value.Name == "FireProjectile")
        weapon.Fire();
};
```

Markers are dispatched when their normalized time is crossed, including when a
loop wraps or a frame crosses more than one loop.

## Root motion

Root motion is disabled by default. Select an imported node and choose a mode:

```csharp
animator.RootMotionNode = "Root";
animator.RootMotionMode = ERootMotionMode.ExtractOnly;
var delta = animator.ConsumeRootMotion();
```

`ExtractOnly` lets physics apply the delta. `ApplyToTransform` moves the model
root and removes the animated root transform from the sampled pose. In-place
locomotion should keep root motion disabled.

## Update order

Gameplay components set parameters in `Update`. `AnimationSceneSystem` evaluates
controllers afterwards, applies local node poses and bone palettes, then normal
`LateUpdate` and rendering continue. Controller and clip data are shared;
parameters, state times, and transition state belong to each `Animator`.

See `AnimationGame` for a buildable demo using the Khronos Fox model.
