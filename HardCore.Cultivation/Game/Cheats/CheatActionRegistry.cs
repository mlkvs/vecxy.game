using System.Reflection;
using Autofac;

namespace HardCore.Cultivation.Game.Cheats;

public sealed class CheatActionRegistry(ILifetimeScope scope)
{
    private readonly Lazy<IReadOnlyList<CheatAction>> _actions = new(() => Discover(scope));

    public IReadOnlyList<CheatAction> Actions => _actions.Value;

    private static IReadOnlyList<CheatAction> Discover(ILifetimeScope scope)
    {
        var assembly = typeof(CheatActionRegistry).Assembly;
        var actions = new List<CheatAction>();
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.Static |
                         BindingFlags.Instance))
            {
                var attribute = method.GetCustomAttribute<CheatActionAttribute>();
                if (attribute is null || method.GetParameters().Length != 0 || method.ContainsGenericParameters)
                    continue;
                object? target = null;
                if (!method.IsStatic && !scope.TryResolve(type, out target))
                    continue;
                actions.Add(new CheatAction(attribute, method, target));
            }
        }
        return actions
            .OrderBy(action => action.GroupOrder)
            .ThenBy(action => action.Group, StringComparer.Ordinal)
            .ThenBy(action => action.Order)
            .ThenBy(action => action.Title, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class CheatAction(CheatActionAttribute attribute, MethodInfo method, object? target)
{
    public string Title => attribute.Title;
    public string Group => attribute.Group;
    public int Order => attribute.Order;
    public int GroupOrder => attribute.GroupOrder;

    public string Invoke()
    {
        var result = method.Invoke(target, null);
        return result switch
        {
            null => Title,
            string message when message.Length > 0 => message,
            _ => result.ToString() ?? Title
        };
    }
}
