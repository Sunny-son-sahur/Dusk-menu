/*
 * SentinelMenu  PluginInfo.cs
 * A wrist-menu for Gorilla Tag built on top of an imgui-style rendering core.
 *
 * Copyright (C) 2026  SentinelMenu
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

namespace SentinelMenu
{
    public class PluginInfo
    {
        public const string GUID = "com.sentinel.gorillatag.sentinelmenu";
        public const string Name = "SentinelMenu";
        public const string Description = "Wrist-mounted mod menu for Gorilla Tag with an imgui-style overlay.";
        public const string BuildTimestamp = "2026-01-01T00:00:00Z";
        public const string Version = "1.0.0";

        // Folder (under BepInEx/plugins) where the menu keeps its runtime files.
        public const string BaseDirectory = "SentinelMenu";

        // Resource path prefix for any embedded assets (icon, fonts, etc.).
        public const string ClientResourcePath = "SentinelMenu.Resources.Client";

#if DEBUG
        public static bool BetaBuild = true;
#else
        public static bool BetaBuild = false;
#endif
    }
}
