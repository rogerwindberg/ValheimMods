﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static FejdStartup;

namespace QuickLoad
{
    [BepInPlugin("aedenthorn.QuickLoad", "Quick Load", "0.6.0")]
    public class QuickLoad: BaseUnityPlugin
    {
        private static readonly bool isDebug = true;

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(QuickLoad).Namespace + " " : "") + str);
        }

        public static ConfigEntry<string> hotKey;
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> autoLoad;
        public static ConfigEntry<int> nexusID;


        private void Awake()
        {
            hotKey = Config.Bind<string>("General", "HotKey", "f7", "Hot key code to perform quick load.");
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            autoLoad = Config.Bind<bool>("General", "AutoLoad", false, "Automatically load into last world");
            nexusID = Config.Bind<int>("General", "NexusID", 7, "Nexus mod ID for updates");

            if (!modEnabled.Value)
                return;

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
        private static bool CheckKeyDown(string value)
        {
            try
            {
                return Input.GetKeyDown(value.ToLower());
            }
            catch
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(FejdStartup), "Start")]
        static class Start_Patch
        {
            static void Postfix(List<PlayerProfile> ___m_profiles, int ___m_profileIndex)
            {
                if (autoLoad.Value)
                {
                    Dbgl("performing auto load");
                    PlayerProfile playerProfile = ___m_profiles[___m_profileIndex];
                    DoQuickLoad(playerProfile.GetFilename(), playerProfile.m_fileSource);
                }
            }
        }

        [HarmonyPatch(typeof(FejdStartup), "Update")]
        static class Update_Patch
        {
            static void Postfix(List<PlayerProfile> ___m_profiles, int ___m_profileIndex)
            {
                if (CheckKeyDown(hotKey.Value))
                {
                    Dbgl("pressed hot key");
                    PlayerProfile playerProfile = ___m_profiles[___m_profileIndex];
                    DoQuickLoad(playerProfile.GetFilename(), playerProfile.m_fileSource);
                }
            }
        }
        private static void DoQuickLoad(string fileName, FileHelpers.FileSource fileSource)
        {

            string worldName = PlayerPrefs.GetString("world");
            Game.SetProfile(fileName, fileSource);

            if (worldName == null || worldName.Length == 0)
                return;

            Dbgl($"got world name {worldName}");

            typeof(FejdStartup).GetMethod("UpdateCharacterList", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(FejdStartup.instance, new object[] { });
            typeof(FejdStartup).GetMethod("UpdateWorldList", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(FejdStartup.instance, new object[] { true });

            bool isOn = FejdStartup.instance.m_publicServerToggle.isOn;
            bool isOn2 = FejdStartup.instance.m_openServerToggle.isOn;
            bool isOn3 = FejdStartup.instance.m_crossplayServerToggle.isOn;
            string text = FejdStartup.instance.m_serverPassword.text;
            World world = (World)typeof(FejdStartup).GetMethod("FindWorld", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(FejdStartup.instance, new object[] { worldName });

            if (world == null)
                return;

            Dbgl($"got world");

            AccessTools.FieldRefAccess<FejdStartup, bool>(FejdStartup.instance, "m_startingWorld") = true;
            ZSteamMatchmaking.instance.StopServerListing();
            PlayerPrefs.SetString("world", world.m_name);
            if (FejdStartup.instance.m_crossplayServerToggle.IsInteractable())
            {
                PlayerPrefs.SetInt("crossplay", FejdStartup.instance.m_crossplayServerToggle.isOn ? 1 : 0);
            }
            ZNet.m_onlineBackend = isOn3 ? OnlineBackendType.PlayFab : OnlineBackendType.Steamworks;
            ZNet.SetServer(true, isOn2, isOn, world.m_name, text, world);
            ZNet.ResetServerHost();
            string eventLabel = "open:" + isOn2.ToString() + ",public:" + isOn.ToString();
            Gogan.LogEvent("Menu", "WorldStart", eventLabel, 0L);
            FejdStartup.StartGameEventHandler startGameEventHandler = AccessTools.FieldRefAccess<FejdStartup, StartGameEventHandler>(FejdStartup.instance, "startGameEvent");
            if (startGameEventHandler != null)
            {
                startGameEventHandler(FejdStartup.instance, new StartGameEventArgs(true));
            }


            Dbgl($"transitioning...");

            typeof(FejdStartup).GetMethod("TransitionToMainScene", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(FejdStartup.instance, new object[] { });
        }
    }
}
