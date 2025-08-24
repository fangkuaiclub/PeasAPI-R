using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PeasAPI.CustomRpc;
using PeasAPI.Roles;
using Reactor.Localization.Utilities;
using Reactor.Utilities;

namespace PeasAPI.Options;

public class CustomOption
{
    public static List<CustomOption> AllOptions = new();

    private static bool _isLoading = false;
    public static int num = 1;
    public readonly int ID;
    public readonly MultiMenu Menu;

    public BaseRole BaseRole;
    public Func<object, string> Format;
    public string Name;
    public bool IsRoleOption;

    public StringNames StringName;

    public CustomOption(int id, MultiMenu menu, string name, CustomOptionType type,
        object defaultValue, object defaultValue2 = null,
        Func<object, string> format = null,
        BaseRole baseRole = null, bool isRoleOption = false)
    {
        ID = id;
        Menu = menu;
        Name = name;
        Type = type;
        DefaultValue = ValueObject = defaultValue;
        DefaultValue2 = ValueObject2 = defaultValue2;
        Format = format ?? (obj => $"{obj}");
        BaseRole = baseRole;

        AllOptions.Add(this);
        Set(ValueObject, ValueObject2);

        StringName = CustomStringName.CreateAndRegister(GetName());

        IsRoleOption = isRoleOption;
    }

    public object ValueObject { get; set; }
    public object ValueObject2 { get; set; }
    public OptionBehaviour Setting { get; set; }
    public CustomOptionType Type { get; set; }
    public object DefaultValue { get; set; }
    public object DefaultValue2 { get; set; }
    public static Func<object, string> Seconds { get; } = value => $"{value:0.0#}s";
    public static Func<object, string> Multiplier { get; } = value => $"{value:0.0#}x";
    public string GetName()
    {
        return Name;
    }

    public override string ToString()
    {
        return Format(ValueObject);
    }

    public string ToString2()
    {
        return Format(ValueObject2);
    }

    public virtual void OptionCreated()
    {
        Setting.name = Setting.gameObject.name = GetName();
    }

    private static string CreateSafeKey(string menu, string name)
    {
        string safeKey = $"{menu}_{name}";
        safeKey = Regex.Replace(safeKey, @"[=\n\t\\""'\[\]]", "_");

        if (string.IsNullOrEmpty(safeKey))
            safeKey = "default_key";

        return safeKey;
    }

    /// <summary>
    /// Saves all current settings to the BepInEx configuration file
    /// </summary>
    public static void SaveSettings()
    {
        if (_isLoading) return;

        try
        {
            bool configChanged = false;

            foreach (var option in AllOptions)
            {
                string safeKey = CreateSafeKey(option.Menu.ToString(), option.Name);

                if (option.Type == CustomOptionType.Role)
                {
                    var valueEntry = PeasAPI.ConfigFile.Bind("Settings", $"{safeKey}_Value",
                        option.DefaultValue?.ToString(),
                        $"Custom option: {option.Name} (Value)");

                    var value2Entry = PeasAPI.ConfigFile.Bind("Settings", $"{safeKey}_Value2",
                        option.DefaultValue2?.ToString(),
                        $"Custom option: {option.Name} (Value2)");

                    if (valueEntry.Value != option.ValueObject?.ToString())
                    {
                        valueEntry.Value = option.ValueObject?.ToString();
                        configChanged = true;
                    }

                    if (value2Entry.Value != option.ValueObject2?.ToString())
                    {
                        value2Entry.Value = option.ValueObject2?.ToString();
                        configChanged = true;
                    }
                }
                else
                {
                    var valueEntry = PeasAPI.ConfigFile.Bind("Settings", safeKey,
                        option.DefaultValue?.ToString(),
                        $"Custom option: {option.Name}");

                    if (valueEntry.Value != option.ValueObject?.ToString())
                    {
                        valueEntry.Value = option.ValueObject?.ToString();
                        configChanged = true;
                    }
                }
            }

            if (configChanged)
            {
                PeasAPI.ConfigFile.Save();
                PeasAPI.Logger.LogInfo("Settings have been saved to the BepInEx configuration file");
            }
        }
        catch (Exception ex)
        {
            PeasAPI.Logger.LogError($"Error occurred while saving settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads settings from the BepInEx configuration file
    /// </summary>
    public static void LoadSettings()
    {
        try
        {
            _isLoading = true;

            foreach (var option in AllOptions)
            {
                string safeKey = CreateSafeKey(option.Menu.ToString(), option.Name);

                try
                {
                    if (option.Type == CustomOptionType.Role)
                    {
                        // Role options have two values
                        var valueEntry = PeasAPI.ConfigFile.Bind("Settings", $"{safeKey}_Value",
                            option.DefaultValue?.ToString(),
                            $"Custom option: {option.Name} (Value)");

                        var value2Entry = PeasAPI.ConfigFile.Bind("Settings", $"{safeKey}_Value2",
                            option.DefaultValue2?.ToString(),
                            $"Custom option: {option.Name} (Value2)");

                        object value = ConvertValue(valueEntry.Value, option.Type);
                        object value2 = ConvertValue(value2Entry.Value, option.Type);

                        option.Set(value, value2, SendRpc: false, Notify: false);
                    }
                    else
                    {
                        // Other options have only one value
                        var valueEntry = PeasAPI.ConfigFile.Bind("Settings", safeKey,
                            option.DefaultValue?.ToString(),
                            $"Custom option: {option.Name}");

                        object value = ConvertValue(valueEntry.Value, option.Type);
                        option.Set(value, null, SendRpc: false, Notify: false);
                    }
                }
                catch (Exception ex)
                {
                    PeasAPI.Logger.LogWarning($"Error occurred while loading option {option.Name}: {ex.Message}");
                }
            }

            PeasAPI.Logger.LogInfo("Settings have been loaded from the BepInEx configuration file");
        }
        catch (Exception ex)
        {
            PeasAPI.Logger.LogError($"Error occurred while loading settings: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static object ConvertValue(string value, CustomOptionType type)
    {
        if (string.IsNullOrEmpty(value)) return null;

        switch (type)
        {
            case CustomOptionType.Toggle:
                return bool.Parse(value);
            case CustomOptionType.Number:
                return float.Parse(value);
            case CustomOptionType.String:
                return int.Parse(value);
            case CustomOptionType.Role:
                return int.Parse(value);
            default:
                return value;
        }
    }

    public void Set(object value, object value2 = null, bool SendRpc = true, bool Notify = false)
    {
        ValueObject = value;
        ValueObject2 = value2;

        if (!_isLoading && AmongUsClient.Instance?.AmHost == true)
        {
            SaveSettings();
        }

        if (Setting != null && AmongUsClient.Instance.AmHost && SendRpc)
            Coroutines.Start(RpcUpdateSetting.SendRpc(this));

        try
        {
            if (Setting is ToggleOption toggle)
            {
                var newValue = (bool)ValueObject;
                toggle.oldValue = newValue;
                if (toggle.CheckMark != null) toggle.CheckMark.enabled = newValue;
            }
            else if (Setting is NumberOption number)
            {
                var newValue = (float)ValueObject;

                number.Value = number.oldValue = newValue;
                number.ValueText.text = ToString();
            }
            else if (Setting is RoleOptionSetting roleOption)
            {
                var newValue = (int)ValueObject;
                var newValue2 = (int)ValueObject2;

                roleOption.roleChance = newValue;
                roleOption.chanceText.text = ToString();

                roleOption.roleMaxCount = newValue2;
                roleOption.countText.text = ToString2();
            }
            else if (Setting is StringOption str)
            {
                var newValue = (int)ValueObject;

                str.Value = str.oldValue = newValue;
                str.ValueText.text = ToString();
            }
        }
        catch
        {
        }

        if (HudManager.InstanceExists && Type != CustomOptionType.Header && Notify)
            if (IsRoleOption)
            {
                HudManager.Instance.Notifier.AddRoleSettingsChangeMessage(StringName, (int)ValueObject2, (int)ValueObject, RoleTeamTypes.Crewmate);
            }
            else
            {
                HudManager.Instance.Notifier.AddSettingsChangeMessage(StringName, ToString(),
                    HudManager.Instance.Notifier.lastMessageKey != (int)StringName);
            }
    }
}