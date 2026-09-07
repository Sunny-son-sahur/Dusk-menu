/*
 * SentinelMenu  Plugin.Injector.cs
 * SharpMonoInjector / legacy native-injection entry. Lets the menu be injected
 * at runtime without a BepInEx plugin scan (kept separate so merging them does
 * not break SMI, which requires methods on a static class).
 *
 * Copyright (C) 2026  SentinelMenu
 */

using SentinelMenu.Managers;
using SentinelMenu.Menu;
using UnityEngine;

namespace SentinelMenu
{
    public static class Plugin
    {
        public static void Inject()
        {
            var go = new GameObject("SentinelMenu");
            go.AddComponent<Injector>();
        }

        public static void InjectDontDestroy()
        {
            var go = new GameObject("SentinelMenu");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Injector>();
        }

        private sealed class Injector : MonoBehaviour
        {
            private void Awake()
            {
                LogManager.SetLogger((Level level, string msg) =>
                {
                    switch (level)
                    {
                        case Level.Error:
                            Debug.LogError(msg);
                            break;
                        case Level.Warning:
                            Debug.LogWarning(msg);
                            break;
                        case Level.Debug:
                            Debug.Log(msg);
                            break;
                        default:
                            Debug.Log(msg);
                            break;
                    }
                });

                Bootstrapper.Initialize();
            }

            private void OnDestroy() =>
                Main.UnloadMenu();
        }
    }
}
