/*
 * SentinelMenu  Managers/InputManager.cs
 * Detects the Y (left secondary) controller button press, plus keyboard and
 * XR device fallbacks, and fires a toggle event on the rising edge.
 *
 * Copyright (C) 2026  SentinelMenu
 */

using System;
using System.Collections.Generic;
using UnityEngine.XR;
using UnityEngine.InputSystem;

namespace SentinelMenu.Managers
{
    public static class InputManager
    {
        public static event Action OnMenuToggle;

        private static InputDevice _leftXRDevice;
        private static bool        _devicesCached;
        private static bool        _lastToggleHeld;

        /// <summary>Check every frame and raise OnMenuToggle on the rising edge.</summary>
        public static void Update()
        {
            if (!_devicesCached)
                CacheXRDevices();

            bool pressed = GetToggle();
            if (pressed && !_lastToggleHeld)
                OnMenuToggle?.Invoke();

            _lastToggleHeld = pressed;
        }

        private static bool GetToggle()
        {
            // Primary source: Gorilla Tag's own controller poller (left secondary = Y).
            try
            {
                if (ControllerInputPoller.instance != null &&
                    ControllerInputPoller.instance.leftControllerSecondaryButton)
                    return true;
            }
            catch { }

            // Fallback: raw XR secondary button on the left hand (Quest / Rift).
            if (_leftXRDevice.isValid &&
                _leftXRDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool xr) &&
                xr)
                return true;

            // Fallback: keyboard Y (handy for testing on desktop / with SteamVR).
            var kb = Keyboard.current;
            if (kb != null && kb.yKey.wasPressedThisFrame)
                return true;

            return false;
        }

        private static void CacheXRDevices()
        {
            var leftList = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftList);
            if (leftList.Count > 0)
                _leftXRDevice = leftList[0];
            _devicesCached = true;
        }
    }
}
