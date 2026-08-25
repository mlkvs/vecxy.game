using HardCore.Cultivation.Game.Presentation;

namespace HardCore.Cultivation.Game.Cheats;

public sealed class CultivationCheats(GameController game)
{
    [CheatAction("+1000 денег", "Ресурсы", order: 10, groupOrder: 10)]
    public string AddMoney()
    {
        game.ChangeMoneyForCheat(1000);
        return "Деньги +1000";
    }

    [CheatAction("-1000 денег", "Ресурсы", order: 20, groupOrder: 10)]
    public string RemoveMoney()
    {
        game.ChangeMoneyForCheat(-1000);
        return "Деньги -1000";
    }

    [CheatAction("+100 душевной силы", "Ресурсы", order: 30, groupOrder: 10)]
    public string AddSpiritualPower()
    {
        game.ChangeSpiritualPowerForCheat(100m);
        return "Душевная сила +100";
    }

    [CheatAction("-100 душевной силы", "Ресурсы", order: 40, groupOrder: 10)]
    public string RemoveSpiritualPower()
    {
        game.ChangeSpiritualPowerForCheat(-100m);
        return "Душевная сила -100";
    }

    [CheatAction("+10 здоровья", "Здоровье", order: 10, groupOrder: 20)]
    public string AddHealth()
    {
        game.ChangeHealthForCheat(10m);
        return "Здоровье +10";
    }

    [CheatAction("-10 здоровья", "Здоровье", order: 20, groupOrder: 20)]
    public string RemoveHealth()
    {
        game.ChangeHealthForCheat(-10m);
        return "Здоровье -10";
    }

    [CheatAction("+10 максимум здоровья", "Здоровье", order: 30, groupOrder: 20)]
    public string AddMaximumHealth()
    {
        game.ChangeMaximumHealthForCheat(10m);
        return "Максимум здоровья +10";
    }

    [CheatAction("-10 максимум здоровья", "Здоровье", order: 40, groupOrder: 20)]
    public string RemoveMaximumHealth()
    {
        game.ChangeMaximumHealthForCheat(-10m);
        return "Максимум здоровья -10";
    }

    [CheatAction("+1 год", "Возраст", order: 10, groupOrder: 30)]
    public string AddAge()
    {
        game.ChangeAgeForCheat(1m);
        return "Возраст +1";
    }

    [CheatAction("-1 год", "Возраст", order: 20, groupOrder: 30)]
    public string RemoveAge()
    {
        game.ChangeAgeForCheat(-1m);
        return "Возраст -1";
    }

    [CheatAction("+10 предел возраста", "Возраст", order: 30, groupOrder: 30)]
    public string AddMaximumAge()
    {
        game.ChangeMaximumAgeForCheat(10m);
        return "Предел возраста +10";
    }

    [CheatAction("-10 предел возраста", "Возраст", order: 40, groupOrder: 30)]
    public string RemoveMaximumAge()
    {
        game.ChangeMaximumAgeForCheat(-10m);
        return "Предел возраста -10";
    }

    [CheatAction("Предыдущая стадия", "Стадия", order: 10, groupOrder: 40)]
    public string PreviousStage()
    {
        var stage = game.ChangeCultivationStageForCheat(-1);
        return $"Стадия: {stage}";
    }

    [CheatAction("Следующая стадия", "Стадия", order: 20, groupOrder: 40)]
    public string NextStage()
    {
        var stage = game.ChangeCultivationStageForCheat(1);
        return $"Стадия: {stage}";
    }

    [CheatAction("Сбросить сохранение", "Сохранение", order: 10, groupOrder: 100)]
    public string ResetSave()
    {
        game.ResetSaveForCheat();
        return "Сохранение сброшено";
    }
}
