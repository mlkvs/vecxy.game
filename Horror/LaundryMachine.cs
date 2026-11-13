using Vecxy.Prototypes;
using Vecxy.Scene;

namespace Horror;

/// <summary>Authorable per-machine settings; visual geometry remains a shared USD asset.</summary>
[EditorComponent("Стиральная машина", Category = "Gameplay", Key = "machine", Description = "Номер, питание и длительность цикла общей модели машинки.")]
public sealed class LaundryMachine : AComponent
{
    public int Number { get; private set; }
    public bool Powered { get; private set; }
    public float CycleSeconds { get; private set; }

    public sealed class Prototype : APrototype<LaundryMachine, Prototype.Options>
    {
        public sealed class Options
        {
            [EditorField("Номер машинки", Min = 0, Order = 0)]
            public int Number { get; set; }
            [EditorField("Питание включено", Order = 1)]
            public bool Powered { get; set; } = true;
            [EditorField("Время стирки", Min = 0.1, Unit = "s", Order = 2, Description = "Длительность цикла в секундах.")]
            public float CycleSeconds { get; set; } = 1320;
        }

        protected override LaundryMachine Instantiate(IPrototypeContext context)
        {
            var ctx = RequireContext<ScenePrototypeContext>(context);
            return (ctx.Object ?? ctx.CreateObject("Laundry machine")).AddComponent<LaundryMachine>();
        }

        protected override void Configure(LaundryMachine target, Options options)
        {
            if (options.Number < 0 || !float.IsFinite(options.CycleSeconds) || options.CycleSeconds <= 0)
                throw new InvalidDataException("Laundry machine number and cycle duration are invalid.");
            target.Number = options.Number;
            target.Powered = options.Powered;
            target.CycleSeconds = options.CycleSeconds;
        }
    }
}
