/*
 * SentinelMenu  Bootstrapper.cs
 * Initialization pipeline: creates runtime directories, waits for the player
 * rig to spawn, then builds the wrist menu.
 *
 * Copyright (C) 2026  SentinelMenu
 */

using SentinelMenu.Menu;
using System.IO;
using UnityEngine;

namespace SentinelMenu
{
    internal static class Bootstrapper
    {
        private static bool initialized;

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            string[] runtimeDirs =
            {
                "",
                "/Config",
                "/Screenshots"
            };

            foreach (string dir in runtimeDirs)
            {
                string target = $"{PluginInfo.BaseDirectory}{dir}";
                if (!Directory.Exists(target))
                    Directory.CreateDirectory(target);
            }

            LogManager.LogInfo($"SentinelMenu {PluginInfo.Version} initializing...");

            // Build the menu once the player rig exists (hand transform for anchoring).
            GorillaTagger.OnPlayerSpawned(LoadMenu);
        }

        private static void LoadMenu()
        {
            var loader = new GameObject("SentinelMenu_Loader");
            loader.AddComponent<Menu.Main>();
            Object.DontDestroyOnLoad(loader);
        }
    }
}
