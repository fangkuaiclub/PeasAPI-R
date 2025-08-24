using System;
using System.Collections.Generic;
using System.Linq;
using PeasAPI.Roles;

namespace PeasAPI.Options;

public class CustomRoleOption : CustomOption
{
    internal CustomOption[] AdvancedOptions;

    public CustomRoleOption(BaseRole baseRole, string prefix, CustomOption[] advancedOptions, MultiMenu menu = MultiMenu.NULL) : base(num++,
        menu == MultiMenu.NULL ? GetMultiMenu(baseRole) : menu,
        Utility.ColorString(baseRole.Color, baseRole.Name), CustomOptionType.Role, baseRole.Chance, baseRole.Count, baseRole: baseRole, isRoleOption: true)
    {
        List<CustomOption> removedOptions = new List<CustomOption>();
        if (advancedOptions != null)
        {
            foreach (var option in advancedOptions)
            {
                if (option != null && CustomOption.AllOptions.Contains(option))
                {
                    removedOptions.Add(option);
                    CustomOption.AllOptions.Remove(option);
                }
            }
        }

        foreach (var option in removedOptions)
        {
            CustomOption.AllOptions.Add(option);
        }

        AdvancedOptions = advancedOptions;
        if (advancedOptions != null)
        {
            foreach (var option in advancedOptions.Where(o => o != null))
            {
                option.Name = $"{prefix}{option.Name}";
            }
        }
    }

    public static Func<object, string> PercentFormat { get; } = value => $"{value:0}%";

    private static MultiMenu GetMultiMenu(BaseRole baseRole)
    {
        switch (baseRole.Team)
        {
            case Team.Role:
                return MultiMenu.Neutral;
            case Team.Alone:
                return MultiMenu.Neutral;
            case Team.Crewmate:
                return MultiMenu.Crewmate;
            case Team.Impostor:
                return MultiMenu.Impostor;
            default:
                return MultiMenu.Main;
        }
    }

    public int ChanceValue => (int)ValueObject;
    public int CountValue => (int)ValueObject2;

    public void IncreaseChance()
    {
        int newChance;
        if (ChanceValue + 10 > 100 + 0.001f)
            newChance = 0;
        else
            newChance = ChanceValue + 10;

        Set(newChance, CountValue);
    }

    public void DecreaseChance()
    {
        int newChance;
        if (ChanceValue - 10 < 0 - 0.001f)
            newChance = 100;
        else
            newChance = ChanceValue - 10;

        Set(newChance, CountValue);
    }

    public void IncreaseCount()
    {
        int newCount;
        if (CountValue + 1 > 15 + 0.001f)
            newCount = 0;
        else
            newCount = CountValue + 1;

        Set(ChanceValue, newCount);
    }

    public void DecreaseCount()
    {
        int newCount;
        if (CountValue - 1 < 0 - 0.001f)
            newCount = 15;
        else
            newCount = CountValue - 1;

        Set(ChanceValue, newCount);
    }

    public int GetChance()
    {
        return ChanceValue;
    }

    public int GetCount()
    {
        return CountValue;
    }

    public override void OptionCreated()
    {
        base.OptionCreated();
        var roleOption = Setting.Cast<RoleOptionSetting>();
        roleOption.roleChance = BaseRole.Chance = (int)ValueObject;
        roleOption.roleMaxCount = (int)ValueObject2;
        roleOption.chanceText.text = ToString();
        roleOption.countText.text = ToString2();
    }
}