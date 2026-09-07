/*
 * SentinelMenu  Menu/Main.cs
 * Top-level MonoBehaviour. Owns the wrist anchor, the toggle input, and the
 * imgui-style overlay that renders the menu on the left hand.
 *
 * Copyright (C) 2026  SentinelMenu
 */

using SentinelMenu.Managers;
using UnityEngine;

namespace SentinelMenu.Menu
{
    public class Main : MonoBehaviour
    {
        public static Main Instance;

        // ── Wrist anchor ──────────────────────────────────────────────
        private Transform  _wristAnchor;   // where the menu attaches (left hand)
        private bool       _rigReady;

        // ── Overlay ───────────────────────────────────────────────────
        private ImGuiOverlay _overlay;
        public static bool   MenuOpen;

        private void Awake()
        {
            Instance = this;

            // Register the mod definitions into MenuState once.
            Mods.RegisterAll();

            InputManager.OnMenuToggle += OnMenuToggle;
            MenuOpen = true;
        }

        private void OnDestroy()
        {
            InputManager.OnMenuToggle -= OnMenuToggle;
        }

        private void OnMenuToggle()
        {
            MenuOpen = !MenuOpen;
            LogManager.LogInfo($"Menu toggled -> {(MenuOpen ? "open" : "closed")}");
        }

        private void Update()
        {
            // Poll toggle input every frame
            InputManager.Update();

            // Wait for rig, then anchor overlay to the left hand.
            if (!_rigReady)
            {
                try
                {
                    var rig = GorillaTagger.Instance?.offlineVRRig;
                    if (rig == null) return;
                    _wristAnchor = rig.leftHandTransform;
                }
                catch { return; }
                if (_wristAnchor == null) return;

                _rigReady = true;
                _overlay = new GameObject("SentinelMenu_Overlay")
                    .AddComponent<ImGuiOverlay>();
                _overlay.Init(_wristAnchor);
                LogManager.LogInfo("Rig found — building overlay");
            }

            if (_overlay != null)
                _overlay.SetVisible(MenuOpen);
        }

        public static void UnloadMenu()
        {
            if (Instance != null)
                Destroy(Instance.gameObject);
            Instance = null;
        }
    }
}
