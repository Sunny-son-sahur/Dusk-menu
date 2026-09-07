/*
 * SentinelMenu  Menu/Mods.cs
 * Registry of mods shown in each category, plus the hook methods that run
 * when a mod is toggled / activated / changed. Replace the placeholder logic
 * here with real gameplay mods.
 *
 * Copyright (C) 2026  SentinelMenu
 */

using SentinelMenu.Menu;

namespace SentinelMenu.Menu
{
    public static class Mods
    {
        public static void RegisterAll()
        {
            // ── Player ────────────────────────────────────────────────
            MenuState.AddMod(0, new ModDef("Speed Boost",   ModType.Toggle, tooltip: "run faster"));
            MenuState.AddMod(0, new ModDef("Jump Power",    ModType.Toggle, tooltip: "higher jumps"));
            MenuState.AddMod(0, new ModDef("Fly",           ModType.Toggle, tooltip: "float away"));
            MenuState.AddMod(0, new ModDef("Long Arms",     ModType.Toggle, tooltip: "stretch arms"));
            MenuState.AddMod(0, new ModDef("Invisible",     ModType.Toggle, tooltip: "hide player model"));
            MenuState.AddMod(0, new ModDef("God Mode",      ModType.Toggle, tooltip: "no damage"));
            MenuState.AddMod(0, new ModDef("Low Gravity",   ModType.Toggle, tooltip: "lunar jump"));
            MenuState.AddMod(0, new ModDef("Anti AFK",      ModType.Toggle));
            MenuState.AddMod(0, new ModDef("Speed Value",   ModType.Slider, 0f, 10f, 2f, "multiplier"));

            // ── Other Player ──────────────────────────────────────────
            MenuState.AddMod(1, new ModDef("Player ESP",    ModType.Toggle, tooltip: "box around players"));
            MenuState.AddMod(1, new ModDef("Name Tags",     ModType.Toggle, tooltip: "show names"));
            MenuState.AddMod(1, new ModDef("Tracers",       ModType.Toggle, tooltip: "line to players"));
            MenuState.AddMod(1, new ModDef("Radar",         ModType.Toggle, tooltip: "minimap"));
            MenuState.AddMod(1, new ModDef("Skeleton ESP",  ModType.Toggle, tooltip: "bones"));

            // ── Items ─────────────────────────────────────────────────
            MenuState.AddMod(2, new ModDef("Item ESP",      ModType.Toggle, tooltip: "spot items"));
            MenuState.AddMod(2, new ModDef("Auto Pickup",   ModType.Toggle, tooltip: "grab automatically"));
            MenuState.AddMod(2, new ModDef("Grab Range",    ModType.Slider, 0.5f, 5f, 1f, "reach"));
            MenuState.AddMod(2, new ModDef("Rapid Fire",    ModType.Toggle, tooltip: "tap faster"));

            // ── Fun ───────────────────────────────────────────────────
            MenuState.AddMod(3, new ModDef("Spin",          ModType.Toggle));
            MenuState.AddMod(3, new ModDef("Dance",         ModType.Toggle));
            MenuState.AddMod(3, new ModDef("Explode",       ModType.Button, tooltip: "boom"));
            MenuState.AddMod(3, new ModDef("Size Changer",  ModType.Slider, 0.1f, 3f, 1f, "scale"));

            // ── World ─────────────────────────────────────────────────
            MenuState.AddMod(4, new ModDef("Full Bright",   ModType.Toggle, tooltip: "no dark rooms"));
            MenuState.AddMod(4, new ModDef("No Fog",        ModType.Toggle));
            MenuState.AddMod(4, new ModDef("Time Scale",    ModType.Slider, 0.1f, 2f, 1f, "speed"));
            MenuState.AddMod(4, new ModDef("Third Person",  ModType.Toggle));

            // ── Settings ──────────────────────────────────────────────
            MenuState.AddMod(5, new ModDef("Theme: Blood Red",   ModType.Button, tooltip: "set theme"));
            MenuState.AddMod(5, new ModDef("Theme: Cyber Blue",  ModType.Button, tooltip: "set theme"));
            MenuState.AddMod(5, new ModDef("Theme: Neon Green",  ModType.Button, tooltip: "set theme"));
            MenuState.AddMod(5, new ModDef("Theme: Purple Haze", ModType.Button, tooltip: "set theme"));
            MenuState.AddMod(5, new ModDef("Reset All",     ModType.Button, tooltip: "disable all"));
        }

        // ── Hooks (called when a mod is clicked) ──────────────────────

        public static void OnModToggled(int category, int index, bool enabled)
        {
            // TODO: implement real mod behavior per mod.
            // Example:
            //   if (name == "Speed Boost") PlayerSpeedModifier = enabled ? ... : ...;
            string name = MenuState.Mods[category][index].Name;
            LogManager.LogInfo($"Mod toggled: {name} -> {(enabled ? "ON" : "OFF")}");
        }

        public static void OnModActivated(int category, int index)
        {
            string name = MenuState.Mods[category][index].Name;
            switch (name)
            {
                case "Theme: Blood Red":
                    MenuState.ActiveThemeIndex = 0;
                    break;
                case "Theme: Cyber Blue":
                    MenuState.ActiveThemeIndex = 1;
                    break;
                case "Theme: Neon Green":
                    MenuState.ActiveThemeIndex = 2;
                    break;
                case "Theme: Purple Haze":
                    MenuState.ActiveThemeIndex = 3;
                    break;
                case "Reset All":
                    foreach (var list in MenuState.Mods)
                        for (int i = 0; i < list.Count; i++)
                        {
                            var def = list[i];
                            def.Enabled = false;
                            def.Value   = def.Min;
                            list[i] = def;
                        }
                    break;
                default:
                    LogManager.LogInfo($"Mod activated: {name}");
                    break;
            }
        }

        public static void OnModChanged(int category, int index, float value)
        {
            string name = MenuState.Mods[category][index].Name;
            LogManager.LogInfo($"Mod changed: {name} -> {value:F2}");
        }
    }
}