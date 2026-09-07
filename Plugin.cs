/*
 * SentinelMenu  Plugin.cs
 * BepInEx entry point. Only wires up the logger and kicks off the bootstrapper.
 *
 * Copyright (C) 2026  SentinelMenu
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using BepInEx;
using SentinelMenu.Managers;
using SentinelMenu.Menu;
using System.ComponentModel;

namespace SentinelMenu
{
    [Description(PluginInfo.Description)]
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class PluginBepInEx : BaseUnityPlugin
    {
        private void Awake()
        {
            LogManager.SetLogger((level, msg) =>
            {
                switch (level)
                {
                    case Level.Error:
                        Logger.LogError(msg);
                        break;
                    case Level.Warning:
                        Logger.LogWarning(msg);
                        break;
                    case Level.Debug:
                        Logger.LogDebug(msg);
                        break;
                    default:
                        Logger.LogInfo(msg);
                        break;
                }
            });

            Bootstrapper.Initialize();
        }

        private void OnDestroy() =>
            Main.UnloadMenu();
    }
}
