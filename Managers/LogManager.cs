/*
 * SentinelMenu  Managers/LogManager.cs
 * Central logging abstraction so the same plugin code works under both
 * BepInEx and native injection (via UnityEngine.Debug).
 *
 * Copyright (C) 2026  SentinelMenu
 */

namespace SentinelMenu.Managers
{
    public enum Level
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public static class LogManager
    {
        public delegate void LogHandler(Level level, string message);
        private static LogHandler _handler;

        public static void SetLogger(LogHandler handler) =>
            _handler = handler;

        public static void LogDebug(string msg) =>
            _handler?.Invoke(Level.Debug, msg);

        public static void LogInfo(string msg) =>
            _handler?.Invoke(Level.Info, msg);

        public static void LogWarning(string msg) =>
            _handler?.Invoke(Level.Warning, msg);

        public static void LogError(string msg) =>
            _handler?.Invoke(Level.Error, msg);
    }
}
