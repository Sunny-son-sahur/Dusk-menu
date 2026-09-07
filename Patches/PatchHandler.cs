/*
 * SentinelMenu  Patches/PatchHandler.cs
 * Central Harmony patch registration. Individual patches live in this folder
 * and are registered here. Currently empty — add [HarmonyPatch] classes and
 * register them here to modify game behavior.
 *
 * Copyright (C) 2026  SentinelMenu
 */

using HarmonyLib;
using SentinelMenu.Managers;

namespace SentinelMenu.Patches
{
    public static class PatchHandler
    {
        private static Harmony _harmony;
        private static bool     _patched;

        public static void PatchAll()
        {
            if (_patched) return;
            _patched = true;

            _harmony = new Harmony(PluginInfo.GUID);
            _harmony.PatchAll();
            LogManager.LogInfo("Harmony patches applied");
        }

        public static void UnpatchAll()
        {
            if (_harmony == null) return;
            _harmony.UnpatchAll(PluginInfo.GUID);
            _harmony = null;
            _patched = false;
        }
    }
}