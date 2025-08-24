using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using BepInEx.Logging;
using HarmonyLib;
using PowerTools;
using Reactor.Utilities;
using Reactor.Utilities.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace PeasAPI.Managers;

public static class CustomHatManager
{
    private static bool LoadedHats;
    internal static readonly Dictionary<string, HatViewData> ViewDataCache = new();
    internal static readonly List<CustomHat> RegisteredHats = new();

    private static ManualLogSource Log => PluginSingleton<PeasAPI>.Instance.Log;

    internal static void LoadHatsRoutine()
    {
        if (LoadedHats || !DestroyableSingleton<HatManager>.InstanceExists ||
            DestroyableSingleton<HatManager>.Instance.allHats.Count == 0)
            return;
        LoadedHats = true;
        Coroutines.Start(LoadHats());
    }

    internal static IEnumerator LoadHats()
    {
        try
        {
            var hatData = new List<HatData>();
            hatData.AddRange(DestroyableSingleton<HatManager>.Instance.allHats);
            hatData.ForEach(x => x.StoreName = "Vanilla");

            var originalCount = DestroyableSingleton<HatManager>.Instance.allHats.ToList().Count;

            // Process registered hats
            for (var i = 0; i < RegisteredHats.Count; i++)
            {
                var customHat = RegisteredHats[i];
                var hatBehaviour = GenerateHatBehaviour(customHat);
                hatBehaviour.StoreName = customHat.ModName;
                hatBehaviour.ProductId = customHat.Name;
                hatBehaviour.name = customHat.Name + $"({customHat.Author})";
                hatBehaviour.Free = true;
                hatBehaviour.displayOrder = originalCount + i;
                hatData.Add(hatBehaviour);
            }

            DestroyableSingleton<HatManager>.Instance.allHats = hatData.ToArray();
        }
        catch (Exception e)
        {
            Log.LogError($"Error while loading hats: {e.Message}\nStack: {e.StackTrace}");
        }

        yield return null;
    }

    public static void RegisterNewHat(string name, Sprite image, Vector2 chipOffset = default,
        bool inFront = true, bool noBounce = true, string author = "Unknown", string modName = "Custom", Sprite climbImage = null,
        Sprite backImage = null, Sprite floorImage = null)
    {
        if (chipOffset == default)
            chipOffset = new Vector2(-0.1f, 0.35f);

        RegisteredHats.Add(new CustomHat
        {
            Name = name,
            Image = image,
            ChipOffset = chipOffset,
            InFront = inFront,
            NoBounce = noBounce,
            Author = author,
            ModName = modName,
            ClimbImage = climbImage,
            BackImage = backImage,
            FloorImage = floorImage
        });
    }

    private static HatData GenerateHatBehaviour(CustomHat customHat)
    {
        Sprite sprite;
        if (HatCache.hatViewDatas.ContainsKey(customHat.Name))
        {
            sprite = HatCache.hatViewDatas[customHat.Name];
        }
        else
        {
            sprite = customHat.Image;
            if (sprite != null)
            {
                HatCache.hatViewDatas.Add(customHat.Name, sprite);
            }
            else
            {
                Log.LogError($"Failed to load hat image: {customHat.Image}");
                return null;
            }
        }

        var hat = ScriptableObject.CreateInstance<HatData>();
        var viewData = ViewDataCache[hat.name] = ScriptableObject.CreateInstance<HatViewData>();

        hat.ChipOffset = customHat.ChipOffset;
        viewData.MainImage = sprite;
        viewData.ClimbImage = customHat.ClimbImage ?? sprite;
        viewData.BackImage = customHat.BackImage ?? sprite;
        viewData.FloorImage = customHat.FloorImage ?? sprite;
        hat.ViewDataRef = new AssetReference(ViewDataCache[hat.name].Pointer);
        hat.InFront = customHat.InFront;
        hat.NoBounce = customHat.NoBounce;

        return hat;
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.Awake))]
    public class AmongUsClient_Patches
    {
        private static bool _executed;

        public static void Prefix()
        {
            if (!_executed)
            {
                LoadHats();
                _executed = true;
            }
        }
    }

    [HarmonyPatch(typeof(HatParent), nameof(HatParent.SetHat), typeof(int))]
    public static class HP_patch
    {
        public static bool Prefix(HatParent __instance, int color)
        {
            if (!HatCache.hatViewDatas.ContainsKey(__instance.Hat.ProductId)) return true;
            __instance.UnloadAsset();
            __instance.PopulateFromViewData();
            __instance.SetMaterialColor(color);
            return false;
        }
    }

    [HarmonyPatch(typeof(HatParent), nameof(HatParent.PopulateFromViewData))]
    public static class PF_patch
    {
        public static bool Prefix(HatParent __instance)
        {
            if (!HatCache.hatViewDatas.ContainsKey(__instance.Hat.ProductId)) return true;
            __instance.UpdateMaterial();
            var hat = HatCache.hatViewDatas[__instance.Hat.ProductId];

            var spriteAnimNodeSync = __instance.SpriteSyncNode ?? __instance.GetComponent<SpriteAnimNodeSync>();
            if ((bool)spriteAnimNodeSync)
                spriteAnimNodeSync.NodeId = __instance.Hat.NoBounce ? 1 : 0;
            if (__instance.Hat.InFront)
            {
                __instance.BackLayer.enabled = false;
                __instance.FrontLayer.enabled = true;
                __instance.FrontLayer.sprite = hat;
            }
            else
            {
                __instance.BackLayer.enabled = true;
                __instance.FrontLayer.enabled = false;
                __instance.FrontLayer.sprite = null;
                __instance.BackLayer.sprite = hat;
            }

            if (!__instance.options.Initialized || !__instance.HideHat())
                return false;
            __instance.FrontLayer.enabled = false;
            __instance.BackLayer.enabled = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(HatParent), nameof(HatParent.SetClimbAnim))]
    public static class PF_climb_patch
    {
        public static bool Prefix(HatParent __instance)
        {
            if (!HatCache.hatViewDatas.ContainsKey(__instance.Hat.ProductId)) return true;
            __instance.FrontLayer.sprite = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.CheckUnlockedItems))]
    public class InventoryManager_Patches
    {
        public static void Prefix()
        {
            LoadHatsRoutine();
        }
    }

    [HarmonyPatch(typeof(HatsTab), nameof(HatsTab.OnEnable))]
    public static class HatsTab_OnEnable
    {
        public static bool Prefix(HatsTab __instance)
        {
            __instance.currentHat =
                DestroyableSingleton<HatManager>.Instance.GetHatById(DataManager.Player.Customization.Hat);
            var allHats = DestroyableSingleton<HatManager>.Instance.GetUnlockedHats();
            var hatGroups = new SortedList<string, List<HatData>>(
                new PaddedComparer<string>("Vanilla", "")
            );
            foreach (var hat in allHats)
            {
                if (!hatGroups.ContainsKey(hat.StoreName))
                    hatGroups[hat.StoreName] = new List<HatData>();
                hatGroups[hat.StoreName].Add(hat);
            }

            foreach (var instanceColorChip in __instance.ColorChips)
                instanceColorChip.gameObject.Destroy();
            __instance.ColorChips.Clear();
            var groupNameText = __instance.GetComponentInChildren<TextMeshPro>(false);
            var hatIdx = 0;
            foreach (var (groupName, hats) in hatGroups)
            {
                var text = Object.Instantiate(groupNameText, __instance.scroller.Inner);
                text.gameObject.transform.localScale = Vector3.one;
                text.GetComponent<TextTranslatorTMP>().Destroy();
                text.text = groupName;
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = 3f;
                text.fontSizeMax = 3f;
                text.fontSizeMin = 0f;

                hatIdx = (hatIdx + 4) / 5 * 5;

                var xLerp = __instance.XRange.Lerp(0.5f);
                var yLerp = __instance.YStart - hatIdx / __instance.NumPerRow * __instance.YOffset;
                text.transform.localPosition = new Vector3(xLerp, yLerp, -1f);
                hatIdx += 5;
                foreach (var hat in hats.OrderBy(HatManager.Instance.allHats.IndexOf))
                {
                    var num = __instance.XRange.Lerp(hatIdx % __instance.NumPerRow / (__instance.NumPerRow - 1f));
                    var num2 = __instance.YStart - hatIdx / __instance.NumPerRow * __instance.YOffset;
                    var colorChip = Object.Instantiate(__instance.ColorTabPrefab, __instance.scroller.Inner);
                    colorChip.transform.localPosition = new Vector3(num, num2, -1f);
                    colorChip.Button.OnClick.AddListener((Action)(() => __instance.SelectHat(hat)));
                    colorChip.Inner.SetHat(hat,
                        __instance.HasLocalPlayer()
                            ? PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId
                            : DataManager.Player.Customization.Color);
                    colorChip.Inner.transform.localPosition = hat.ChipOffset + new Vector2(0f, -0.3f);
                    colorChip.Tag = hat;
                    __instance.ColorChips.Add(colorChip);
                    hatIdx += 1;
                }
            }

            __instance.scroller.ContentYBounds.max =
                -(__instance.YStart - (hatIdx + 1) / __instance.NumPerRow * __instance.YOffset) - 3f;
            __instance.currentHatIsEquipped = true;

            return false;
        }
    }

    public static class HatCache
    {
        public static Dictionary<string, Sprite> hatViewDatas = new();
    }

    public class PaddedComparer<T> : IComparer<T> where T : IComparable
    {
        private readonly T[] _forcedToBottom;

        public PaddedComparer(params T[] forcedToBottom)
        {
            _forcedToBottom = forcedToBottom;
        }

        public int Compare(T x, T y)
        {
            if (_forcedToBottom.Contains(x) && _forcedToBottom.Contains(y))
                return StringComparer.InvariantCulture.Compare(x, y);

            if (_forcedToBottom.Contains(x))
                return 1;
            if (_forcedToBottom.Contains(y))
                return -1;

            return StringComparer.InvariantCulture.Compare(x, y);
        }
    }
}

// New class to store custom hat data
public class CustomHat
{
    public string Name { get; set; }
    public Sprite Image { get; set; }
    public Vector2 ChipOffset { get; set; }
    public bool InFront { get; set; }
    public bool NoBounce { get; set; }
    public string Author { get; set; }
    public string ModName { get; set; }
    public Sprite ClimbImage { get; set; }
    public Sprite BackImage { get; set; }
    public Sprite FloorImage { get; set; }
}
