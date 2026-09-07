/*
 * SentinelMenu  Menu/MenuState.cs
 * Static registry for categories, mod buttons, and their enabled state.
 * The imgui renderer reads this; individual mods flip the booleans.
 *
 * Copyright (C) 2026  SentinelMenu
 */

using System.Collections.Generic;
using UnityEngine;

namespace SentinelMenu.Menu
{
    public enum ModType
    {
        Toggle,   // simple on/off switch
        Slider,   // value from 0..1 mapped by the mod
        Button    // fire-once action
    }

    public struct ModDef
    {
        public string  Name;
        public ModType Type;

        // Toggle state (persisted per mod)
        public bool     Enabled;

        // Slider state
        public float    Value;
        public float    Min;
        public float    Max;

        // ImGui-style optional description / help text
        public string   Tooltip;

        public ModDef(string name, ModType type, float min = 0f, float max = 1f, float value = 0f, string tooltip = "")
        {
            Name     = name;
            Type     = type;
            Enabled  = false;
            Value    = value;
            Min      = min;
            Max      = max;
            Tooltip  = tooltip;
        }
    }

    public static class MenuState
    {
        public static readonly string[] Categories =
        {
            "Player",
            "Other Player",
            "Items",
            "Fun",
            "World",
            "Settings"
        };

        // categories[categoryIndex][modIndex] -> ModDef
        public static List<List<ModDef>> Mods = new List<List<ModDef>>();

        public static int ActiveCategory;

        // Theme slots, read by the renderer
        public static readonly string[] Themes = { "Blood Red", "Cyber Blue", "Neon Green", "Purple Haze" };
        public static int ActiveThemeIndex;

        static MenuState()
        {
            for (int i = 0; i < Categories.Length; i++)
                Mods.Add(new List<ModDef>());
        }

        public static void AddMod(int category, ModDef mod)
        {
            if (category >= 0 && category < Mods.Count)
                Mods[category].Add(mod);
        }

        public static ModDef GetMod(int category, int index) =>
            Mods[category][index];

        public static void ToggleMod(int category, int index)
        {
            var def = Mods[category][index];
            def.Enabled = !def.Enabled;
            Mods[category][index] = def;
        }

        public static int ActiveModCount()
        {
            int count = 0;
            foreach (var list in Mods)
                foreach (var mod in list)
                    if (mod.Enabled)
                        count++;
            return count;
        }

        public static Color AccentColor()
        {
            switch (ActiveThemeIndex)
            {
                case 0:  return new Color(0.80f, 0.10f, 0.10f, 1f); // Blood Red
                case 1:  return new Color(0.10f, 0.40f, 0.90f, 1f); // Cyber Blue
                case 2:  return new Color(0.10f, 0.80f, 0.20f, 1f); // Neon Green
                default: return new Color(0.50f, 0.10f, 0.80f, 1f); // Purple Haze
            }
        }
    }
}
