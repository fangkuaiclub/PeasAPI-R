using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System;
using UnityEngine.AddressableAssets;
using AmongUs.Data;
using Innersloth.Assets;
using Reactor.Utilities.Extensions;
using Reactor.Utilities;
using TMPro;

namespace PeasAPI.Managers
{
    public static class CustomVisorManager
    {
        public static Material MagicShader = new Material(Shader.Find("Unlit/PlayerShader"));

        public struct CustomVisorData
        {
            public string Name;
            public string Author;
            public Sprite Image;
            public Sprite ClimbImage;
            public Sprite FloorImage;
            public Vector2 ChipOffset;
            public bool MatchPlayerColor;
            public string Group;
        }

        public static bool _customVisorLoaded = false;
        static readonly List<VisorData> visorData = new();
        private static readonly List<CustomVisors> customVisorData = new();
        public static readonly Dictionary<string, VisorViewData> CustomVisorViewDatas = [];
        public static readonly Dictionary<string, List<CustomVisorData>> RegisteredVisors = new();

        public static void RegisterNewVisor(string name, Sprite image, Vector2 chipOffset = new Vector2(),
                                          Sprite climbImage = null, Sprite floorImage = null,
                                          bool matchPlayerColor = false, string author = "Unknown", string group = "Custom")
        {
            if (!RegisteredVisors.ContainsKey(group))
            {
                RegisteredVisors[group] = new List<CustomVisorData>();
            }

            RegisteredVisors[group].Add(new CustomVisorData
            {
                Name = name,
                Author = author,
                Image = image,
                ClimbImage = climbImage,
                FloorImage = floorImage,
                ChipOffset = chipOffset,
                MatchPlayerColor = matchPlayerColor,
                Group = group
            });
        }

        [HarmonyPatch(typeof(HatManager), nameof(HatManager.GetVisorById))]
        class AddCustomVisorsPatch
        {
            public static void Postfix(HatManager __instance)
            {
                if (_customVisorLoaded) return;
                _customVisorLoaded = true;
                var AllVisors = __instance.allVisors.ToList();

                foreach (var group in RegisteredVisors)
                {
                    foreach (var data in group.Value)
                    {
                        VisorViewData vvd = new VisorViewData();
                        vvd.IdleFrame = data.Image;
                        vvd.LeftIdleFrame = data.ClimbImage;
                        vvd.FloorFrame = data.FloorImage;
                        vvd.MatchPlayerColor = data.MatchPlayerColor;

                        var visor = new CustomVisors(vvd);
                        visor.name = $"{data.Name} (by {data.Author})";
                        visor.ProductId = "lmj_" + visor.name.Replace(' ', '_');
                        visor.BundleId = "lmj_" + visor.name.Replace(' ', '_');
                        visor.displayOrder = 99;
                        visor.ChipOffset = data.ChipOffset;
                        visor.Free = true;
                        visorData.Add(visor);
                        customVisorData.Add(visor);
                        var assetRef = new AssetReference(vvd.Pointer);
                        visor.ViewDataRef = assetRef;
                        visor.CreateAddressableAsset();
                        CustomVisorViewDatas.TryAdd(visor.ProductId, vvd);
                    }
                }

                AllVisors.AddRange(visorData);
                __instance.allVisors = AllVisors.ToArray();
            }
        }

        [HarmonyPatch(typeof(VisorsTab), nameof(VisorsTab.OnEnable))]
        public static class VisorsTabOnEnablePatch
        {
            private static TMP_Text Template;

            private static float CreateVisorPackage(List<VisorData> visors, string packageName, float YStart, VisorsTab __instance)
            {

                var offset = YStart;

                if (Template)
                {
                    var title = UnityEngine.Object.Instantiate(Template, __instance.scroller.Inner);
                    var material = title.GetComponent<MeshRenderer>().material;
                    material.SetFloat("_StencilComp", 4f);
                    material.SetFloat("_Stencil", 1f);
                    title.transform.localPosition = new(2.25f, YStart, -1f);
                    title.transform.localScale = Vector3.one * 1.5f;
                    title.fontSize *= 0.5f;
                    title.enableAutoSizing = false;
                    Coroutines.Start(Utility.PerformTimedAction(0.1f, _ => title.SetText(packageName, true)));
                    offset -= 0.8f * __instance.YOffset;
                }

                for (var i = 0; i < visors.Count; i++)
                {
                    var visor = visors[i];
                    var xpos = __instance.XRange.Lerp(i % __instance.NumPerRow / (__instance.NumPerRow - 1f));
                    var ypos = offset - (i / __instance.NumPerRow * __instance.YOffset);
                    var colorChip = UnityEngine.Object.Instantiate(__instance.ColorTabPrefab, __instance.scroller.Inner);

                    if (ActiveInputManager.currentControlType == ActiveInputManager.InputType.Keyboard)
                    {
                        colorChip.Button.OverrideOnMouseOverListeners(() => __instance.SelectVisor(visor));
                        colorChip.Button.OverrideOnMouseOutListeners(() => __instance.SelectVisor(HatManager.Instance.GetVisorById(DataManager.Player.Customization.Visor)));
                        colorChip.Button.OverrideOnClickListeners(__instance.ClickEquip);
                    }
                    else
                        colorChip.Button.OverrideOnClickListeners(() => __instance.SelectVisor(visor));

                    colorChip.Button.ClickMask = __instance.scroller.Hitbox;
                    colorChip.transform.localPosition = new(xpos, ypos, -1f);
                    colorChip.Inner.SetMaskType(PlayerMaterial.MaskType.SimpleUI);
                    colorChip.Inner.transform.localPosition = visor.ChipOffset;
                    colorChip.ProductId = visor.ProductId;
                    colorChip.Tag = visor;
                    __instance.UpdateMaterials(colorChip.Inner.FrontLayer, visor);
                    var colorId = __instance.HasLocalPlayer() ? PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId : DataManager.Player.Customization.Color;

                    if (CustomVisorViewDatas.TryGetValue(visor.ProductId, out var data))
                        ColorChipFix(colorChip, data.IdleFrame, colorId);
                    else
                        visor.SetPreview(colorChip.Inner.FrontLayer, colorId);

                    colorChip.SelectionHighlight.gameObject.SetActive(false);
                    __instance.ColorChips.Add(colorChip);
                }

                return offset - ((visors.Count - 1) / __instance.NumPerRow * __instance.YOffset) - 1.5f;
            }

            private static void ColorChipFix(ColorChip chip, Sprite sprite, int colorId)
            {
                chip.Inner.FrontLayer.sprite = sprite;
                AddressableAssetHandler.AddToGameObject(chip.Inner.FrontLayer.gameObject);

                if (Application.isPlaying)
                    PlayerMaterial.SetColors(colorId, chip.Inner.FrontLayer);
            }

            public static bool Prefix(VisorsTab __instance)
            {
                for (var i = 0; i < __instance.scroller.Inner.childCount; i++)
                    __instance.scroller.Inner.GetChild(i).gameObject.Destroy();

                __instance.ColorChips = new();
                var array = HatManager.Instance.GetUnlockedVisors();
                var packages = new Dictionary<string, List<VisorData>>();

                foreach (var data in array)
                {
                    var package = "Innersloth";

                    if (data.ProductId.StartsWith("lmj_"))
                    {
                        package = "Custom";
                        foreach (var group in RegisteredVisors)
                        {
                            if (customVisorData.Any(v => v.ProductId == data.ProductId &&
                                group.Value.Any(vd => $"{vd.Name} (by {vd.Author})" == v.name)))
                            {
                                package = group.Key;
                                break;
                            }
                        }
                    }

                    if (!packages.ContainsKey(package))
                        packages[package] = [];

                    packages[package].Add(data);
                }

                var yOffset = __instance.YStart;
                Template = __instance.transform.FindChild("Text").gameObject.GetComponent<TMP_Text>();
                var keys = packages.Keys.OrderBy(x => x switch {
                    "Innersloth" => 999,
                    _ => Array.IndexOf(RegisteredVisors.Keys.ToArray(), x)
                });

                keys.ForEach(key => yOffset = CreateVisorPackage(packages[key], key, yOffset, __instance));

                if (array.Length != 0)
                    __instance.GetDefaultSelectable().PlayerEquippedForeground.SetActive(true);

                __instance.visorId = DataManager.Player.Customization.Visor;
                __instance.currentVisorIsEquipped = true;
                __instance.SetScrollerBounds();
                __instance.scroller.ContentYBounds.max = -(yOffset + 4.1f);
                return false;
            }
        }

        [HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.GetVisor))]
        class CosmeticsCacheGetVisorPatch
        {
            public static bool Prefix(CosmeticsCache __instance, string id, ref VisorViewData __result)
            {
                if (!id.StartsWith("lmj_")) return true;
                __result = GetByCache(id);
                if (__result == null)
                    __result = __instance.visors["visor_EmptyVisor"].GetAsset();
                return false;
            }
        }

        static Dictionary<string, VisorViewData> cache = new();
        static VisorViewData GetByCache(string id)
        {
            if (!cache.ContainsKey(id))
            {
                cache[id] = customVisorData.FirstOrDefault(x => x.ProductId == id)?.visorViewData;
            }
            return cache[id];
        }

        [HarmonyPatch(typeof(VisorLayer), nameof(VisorLayer.UpdateMaterial))]
        class VisorLayerUpdateMaterialPatch
        {
            public static bool Prefix(VisorLayer __instance)
            {
                if (__instance.visorData == null || !__instance.visorData.ProductId.StartsWith("lmj_")) return true;
                VisorViewData asset = GetByCache(__instance.visorData.ProductId);
                if (asset == null) return true;

                PlayerMaterial.MaskType maskType = __instance.matProperties.MaskType;
                if (asset.MatchPlayerColor)
                {
                    if (maskType == PlayerMaterial.MaskType.ComplexUI || maskType == PlayerMaterial.MaskType.ScrollingUI)
                    {
                        __instance.Image.sharedMaterial = DestroyableSingleton<HatManager>.Instance.MaskedPlayerMaterial;
                    }
                    else
                    {
                        __instance.Image.sharedMaterial = DestroyableSingleton<HatManager>.Instance.PlayerMaterial;
                    }
                }
                else if (maskType == PlayerMaterial.MaskType.ComplexUI || maskType == PlayerMaterial.MaskType.ScrollingUI)
                {
                    __instance.Image.sharedMaterial = DestroyableSingleton<HatManager>.Instance.MaskedMaterial;
                }
                else
                {
                    __instance.Image.sharedMaterial = DestroyableSingleton<HatManager>.Instance.DefaultShader;
                }
                switch (maskType)
                {
                    case PlayerMaterial.MaskType.SimpleUI:
                        __instance.Image.maskInteraction = (SpriteMaskInteraction)1;
                        break;
                    case PlayerMaterial.MaskType.Exile:
                        __instance.Image.maskInteraction = (SpriteMaskInteraction)2;
                        break;
                    default:
                        __instance.Image.maskInteraction = (SpriteMaskInteraction)0;
                        break;
                }
                __instance.Image.material.SetInt(PlayerMaterial.MaskLayer, __instance.matProperties.MaskLayer);
                if (asset.MatchPlayerColor)
                {
                    PlayerMaterial.SetColors(__instance.matProperties.ColorId, __instance.Image);
                }
                if (__instance.matProperties.MaskLayer <= 0)
                {
                    PlayerMaterial.SetMaskLayerBasedOnLocalPlayer(__instance.Image, __instance.matProperties.IsLocalPlayer);
                    return false;
                }
                __instance.Image.material.SetInt(PlayerMaterial.MaskLayer, __instance.matProperties.MaskLayer);
                return false;
            }
        }

        [HarmonyPatch(typeof(VisorLayer), nameof(VisorLayer.SetFlipX))]
        class VisorLayerSetFlipXPatch
        {
            public static bool Prefix(VisorLayer __instance, bool flipX)
            {
                if (__instance.visorData == null || !__instance.visorData.ProductId.StartsWith("lmj_")) return true;
                VisorViewData asset = GetByCache(__instance.visorData.ProductId);
                if (asset == null) return true;

                __instance.Image.flipX = flipX;
                if (flipX && asset.LeftIdleFrame)
                {
                    __instance.Image.sprite = asset.LeftIdleFrame;
                }
                else
                {
                    __instance.Image.sprite = asset.IdleFrame;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(VisorLayer), nameof(VisorLayer.SetFloorAnim))]
        class VisorLayerSetVisorFloorPositionPatch
        {
            public static bool Prefix(VisorLayer __instance)
            {
                if (__instance.visorData == null || !__instance.visorData.ProductId.StartsWith("lmj_")) return true;
                VisorViewData asset = GetByCache(__instance.visorData.ProductId);
                if (asset == null) return true;

                __instance.Image.sprite = asset.FloorFrame ? asset.FloorFrame : asset.IdleFrame;
                return false;
            }
        }

        [HarmonyPatch(typeof(VisorLayer), nameof(VisorLayer.PopulateFromViewData))]
        class VisorLayerPopulateFromViewDataPatch
        {
            public static bool Prefix(VisorLayer __instance)
            {
                if (__instance.visorData == null || !__instance.visorData.ProductId.StartsWith("lmj_"))
                    return true;
                __instance.UpdateMaterial();
                if (!__instance.IsDestroyedOrNull())
                {
                    __instance.transform.SetLocalZ(__instance.DesiredLocalZPosition);
                    __instance.SetFlipX(__instance.Image.flipX);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(VisorLayer), nameof(VisorLayer.SetVisor), new Type[] { typeof(VisorData), typeof(int) })]
        class VisorLayerSetVisorPatch
        {
            public static bool Prefix(VisorLayer __instance, VisorData data, int color)
            {
                if (!data.ProductId.StartsWith("lmj_")) return true;
                __instance.visorData = data;
                __instance.SetMaterialColor(color);
                __instance.PopulateFromViewData();
                return false;
            }
        }
    }

    class CustomVisors : VisorData
    {
        public VisorViewData visorViewData;
        public CustomVisors(VisorViewData hvd)
        {
            visorViewData = hvd;
        }
    }
}