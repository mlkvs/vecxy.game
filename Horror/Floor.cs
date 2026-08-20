using System.Numerics;
using Vecxy.Physics;
using Vecxy.Rendering;
using Vecxy.Scene;

namespace Horror;

public sealed class Floor : AComponent
{
    public sealed class Prototype : APrototype<Floor, Prototype.Options>
    {
        public sealed class Options : IPrototype.IOptions
        {
            public Vector3 Position { get; init; }
            public Vector2 Size { get; init; } = new(20.0f, 20.0f);
            public float Thickness { get; init; } = 1.0f;
            public Mesh? Mesh { get; init; }
            public Material? Material { get; init; }
        }

        protected override Floor Instantiate(InstantiateContext ctx)
        {
            if (ctx.Scene is null)
                throw new InvalidOperationException("Floor requires a scene.");

            return ctx.Scene.CreateObject("Floor", isStatic: true)
                .AddComponent<Floor>();
        }

        protected override void Configure(Floor floor, Options options)
        {
            if (options.Size.X <= 0.0f ||
                options.Size.Y <= 0.0f ||
                options.Thickness <= 0.0f ||
                options.Mesh is null ||
                options.Material is null)
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            floor.Transform.Position = options.Position;
            floor.Transform.Scale = new Vector3(
                options.Size.X,
                1.0f,
                options.Size.Y);

            var collider = floor.SceneObject!.AddComponent<BoxCollider>();
            collider.Size = new Vector3(1.0f, options.Thickness, 1.0f);
            collider.Center = new Vector3(0.0f, -options.Thickness * 0.5f, 0.0f);

            floor._mesh = options.Mesh;
            floor.SceneObject.AddComponent<MeshRenderer>()
                .SetMesh(floor._mesh, options.Material);
        }
    }

    private Mesh? _mesh;

    public override void OnDestroy()
    {
        _mesh?.Dispose();
        _mesh = null;
    }
}
