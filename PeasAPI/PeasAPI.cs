﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using InnerNet;
using PeasAPI.Components;
using PeasAPI.GameModes;
using PeasAPI.Managers;
using PeasAPI.Options;
using Reactor;
using UnityEngine;
using static PeasAPI.Managers.WatermarkManager;
using Random = System.Random;

namespace PeasAPI
{
    [HarmonyPatch]
    [BepInPlugin(Id, "PeasAPI", Version)]
    [BepInProcess("Among Us.exe")]
    [BepInDependency(ReactorPlugin.Id)]
    public class PeasAPI : BasePlugin
    {
        public const string Id = "tk.peasplayer.amongus.api";
        public const string Version = "1.9.0";

        public Harmony Harmony { get; } = new Harmony(Id);

        public static readonly Random Random = new Random();

        public static ConfigFile ConfigFile { get; private set; }

        public static ManualLogSource Logger { get; private set; }

        public static bool Logging
        {
            get
            {
                if (ConfigFile == null)
                    return true;
                return ConfigFile.Bind("Settings", "Logging", true).Value;
            }
        }

        public static bool GameStarted
        {
            get
            {
                return GameData.Instance && ShipStatus.Instance && AmongUsClient.Instance &&
                       (AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started ||
                        AmongUsClient.Instance.NetworkMode == global::NetworkModes.FreePlay);
            }
        }

        /// <summary>
        /// If you set this to false please provide credit! I mean this stuff is free and open-source so a little credit would be nice :)
        /// </summary>
        public static bool ShamelessPlug = true;

        public static CustomToggleOption ShowRolesOfDead;

        public override void Load()
        {
            Logger = this.Log;
            ConfigFile = Config;

            var useCustomServer = ConfigFile.Bind("CustomServer", "UseCustomServer", false);
            if (useCustomServer.Value)
            {
                CustomServerManager.RegisterServer(ConfigFile.Bind("CustomServer", "Name", "CustomServer").Value,
                    ConfigFile.Bind("CustomServer", "Ipv4 or Hostname", "au.peasplayer.tk").Value,
                    ConfigFile.Bind("CustomServer", "Port", (ushort)22023).Value);
            }

            UpdateManager.RegisterGitHubUpdateListener("Peasplayer", "PeasAPI");

            RegisterCustomRoleAttribute.Load();
            RegisterCustomGameModeAttribute.Load();

            new CustomHeaderOption(MultiMenu.Main, "General Settings");
            ShowRolesOfDead =
                new CustomToggleOption(MultiMenu.Main, "Show the roles of dead player", false);
            GameModeManager.GameModeOption = new CustomStringOption(MultiMenu.Main, "GameMode", new string[] { "None" });
            
            Harmony.PatchAll();
        }

        [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
        [HarmonyPrefix]
        public static void PatchToTestSomeStuff(KeyboardJoystick __instance)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
            }
        }
    }
}