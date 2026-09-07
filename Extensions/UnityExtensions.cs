/*
 * SentinelMenu  Extensions/UnityExtensions.cs
 * Micro-extensions that keep the menu code terse.
 *
 * Copyright (C) 2026  SentinelMenu
 */

using UnityEngine;

namespace SentinelMenu.Extensions
{
    public static class UnityExtensions
    {
        /// <summary>Persist a GameObject across scene loads if the plugin is alive.</summary>
        public static GameObject DontDestroy(this GameObject go)
        {
            Object.DontDestroyOnLoad(go);
            return go;
        }

        /// <summary>True if a component exists, otherwise attach it.</summary>
        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null)
                comp = go.AddComponent<T>();
            return comp;
        }
    }
}