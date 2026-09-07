/*
 * SentinelMenu  Menu/ImGuiOverlay.cs
 * The rendering core. Builds a wrist-anchored world-space canvas with an
 * imgui-style look: dark window, header bar, sidebar category tabs, and a
 * content area of toggle/slider/button rows. No external ImGui dependency —
 * it's Unity UGUI styled to read like imgui, exactly like the reference
 * menus (dark, dense, accent-highlighted).
 *
 * Copyright (C) 2026  SentinelMenu
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SentinelMenu.Menu
{
    public class ImGuiOverlay : MonoBehaviour
    {
        // ── Layout constants ──────────────────────────────────────────
        private const int   ModsPerPage  = 8;
        private const float RowHeight    = 16f;
        private const float SidebarPct   = 0.28f;
        private const float HeaderH      = 26f;
        private const float SpinePadding = 2f;

        // ── Canvas tree ───────────────────────────────────────────────
        private Transform  _anchor;       // wrist transform
        private GameObject _canvasGO;
        private Canvas     _canvas;
        private Material   _uiMat;

        private GameObject _bg;
        private Text       _titleText;
        private Text       _countText;
        private Text       _pageText;

        private readonly List<GameObject> _sidebarBtns = new List<GameObject>();
        private readonly List<GameObject> _modRows     = new List<GameObject>();

        private int _pageStart;
        private bool _visible = true;
        private bool _built;

        // Palette
        private Color Accent       => MenuState.AccentColor();
        private Color TextPrimary  => Color.white;
        private Color TextSecondary=> new Color(0.85f, 0.85f, 0.85f, 0.9f);
        private Color RowAltColor  => new Color(1f, 1f, 1f, 0.04f);
        private Color WindowBg     => new Color(0.09f, 0.09f, 0.10f, 0.96f);
        private Color SidebarBg    => new Color(0.05f, 0.05f, 0.06f, 0.98f);
        private Color HeaderBg     => new Color(0.13f, 0.13f, 0.14f, 1f);
        private Color ButtonBg     => new Color(0.18f, 0.18f, 0.20f, 1f);
        private Color BorderColor  => new Color(1f, 1f, 1f, 0.08f);

        // ── Public API ────────────────────────────────────────────────
        public void Init(Transform wristAnchor)
        {
            _anchor = wristAnchor;
            BuildCanvas();
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_canvasGO != null)
                _canvasGO.SetActive(visible);
        }

        // ── Build ─────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            _canvasGO = new GameObject("SentinelMenu_Canvas");
            DontDestroyOnLoad(_canvasGO);

            _canvas = _canvasGO.AddComponent<Canvas>();
            _canvas.renderMode    = RenderMode.WorldSpace;
            _canvas.sortingOrder  = 999;

            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var rt = _canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360f, 420f);

            _canvasGO.AddComponent<GraphicRaycaster>();

            _uiMat = GetUIMaterial();

            // ── Window body ───────────────────────────────────────────
            _bg = CreatePanel("Body", _canvasGO.transform, WindowBg,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Border line (imgui-style 1px frame)
            CreatePanel("BorderLeft",  _bg.transform, BorderColor,
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), Vector2.zero).GetComponent<RectTransform>().sizeDelta = new Vector2(1f, 0f);
            CreatePanel("BorderTop",   _bg.transform, BorderColor,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero).GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 1f);
            CreatePanel("BorderRight", _bg.transform, BorderColor,
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(1f, 0.5f), Vector2.zero).GetComponent<RectTransform>().sizeDelta = new Vector2(1f, 0f);
            CreatePanel("BorderBottom",_bg.transform, BorderColor,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero).GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 1f);

            // ── Header (title bar) ────────────────────────────────────
            var header = CreatePanel("Header", _bg.transform, HeaderBg,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero);
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, HeaderH);

            _titleText = CreateText(header.transform, "SentinelMenu " + PluginInfo.Version, 10,
                FontStyle.Bold, TextAnchor.MiddleLeft, TextPrimary,
                new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(6f, 0f));
            _titleText.horizontalOverflow = HorizontalWrapMode.Overflow;

            _countText = CreateText(header.transform, "0 active", 9,
                FontStyle.Normal, TextAnchor.MiddleRight, TextSecondary,
                new Vector2(0.6f, 0f), new Vector2(1f, 1f), new Vector2(-6f, 0f));

            // ── Sidebar (category tabs) ───────────────────────────────
            var sidebar = CreatePanel("Sidebar", _bg.transform, SidebarBg,
                new Vector2(0f, 0f), new Vector2(SidebarPct, 1f),
                new Vector2(0f, 0f), new Vector2(0f, -HeaderH));
            BuildSidebar(sidebar.transform);

            // ── Content ───────────────────────────────────────────────
            var content = CreatePanel("Content", _bg.transform, Color.clear,
                new Vector2(SidebarPct, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(SpinePadding, -HeaderH));
            BuildContent(content.transform);

            _built = true;
            RebuildContent();
            SetVisible(_visible);
        }

        // ── Sidebar ───────────────────────────────────────────────────
        private void BuildSidebar(Transform parent)
        {
            float y = -2f;
            for (int i = 0; i < MenuState.Categories.Length; i++)
            {
                int idx = i;
                var panel = CreatePanel("Cat_" + i, parent, ButtonBg,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, y));
                panel.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 20f);

                CreateText(panel.transform, MenuState.Categories[i], 7,
                    FontStyle.Normal, TextAnchor.MiddleLeft, TextPrimary,
                    new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f));

                var btn = panel.AddComponent<Button>();
                var cb  = btn.colors;
                cb.normalColor      = Color.clear;
                cb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
                cb.pressedColor     = new Color(1f, 1f, 1f, 0.14f);
                btn.colors = cb;
                btn.onClick.AddListener(() =>
                {
                    MenuState.ActiveCategory = idx;
                    _pageStart = 0;
                    RebuildContent();
                });

                _sidebarBtns.Add(panel);
                y -= 21f;
            }
        }

        // ── Content ───────────────────────────────────────────────────
        private void BuildContent(Transform parent)
        {
            // Page nav ▲
            CreateNavButton(parent, "▲", new Vector2(0f, 1f), new Vector2(0.5f, 1f),
                () => { _pageStart = Mathf.Max(0, _pageStart - ModsPerPage); RebuildContent(); });

            _pageText = CreateText(parent, "1/1", 8,
                FontStyle.Normal, TextAnchor.MiddleCenter, TextSecondary,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1.5f));

            // Page nav ▼
            CreateNavButton(parent, "▼", new Vector2(0.5f, 1f), new Vector2(1f, 1f),
                () =>
                {
                    int max = Mathf.Max(0, MenuState.Mods[MenuState.ActiveCategory].Count - ModsPerPage);
                    _pageStart = Mathf.Min(max, _pageStart + ModsPerPage);
                    RebuildContent();
                });
        }

        private void CreateNavButton(Transform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            var go = CreatePanel("Nav_" + label, parent, ButtonBg,
                anchorMin, anchorMax, new Vector2(0.5f, 1f), new Vector2(0f, -6f));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 14f);

            CreateText(go.transform, label, 8,
                FontStyle.Normal, TextAnchor.MiddleCenter, TextPrimary,
                Vector2.zero, Vector2.one, Vector2.zero);

            var btn = go.AddComponent<Button>();
            var cb  = btn.colors;
            cb.normalColor      = Color.clear;
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            cb.pressedColor     = new Color(1f, 1f, 1f, 0.14f);
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick());
        }

        // ── Rebuild content rows ──────────────────────────────────────
        private void RebuildContent()
        {
            if (!_built) return;

            // Wipe old rows
            foreach (var row in _modRows)
                if (row != null) Destroy(row);
            _modRows.Clear();

            // Sidebar highlight
            for (int i = 0; i < _sidebarBtns.Count; i++)
            {
                var btn = _sidebarBtns[i]?.GetComponent<Button>();
                if (btn == null) continue;
                var cb = btn.colors;
                cb.normalColor = i == MenuState.ActiveCategory ? Accent : new Color(0.15f, 0.15f, 0.15f, 1f);
                btn.colors = cb;
            }

            // Count active
            if (_countText != null)
                _countText.text = $"{MenuState.ActiveModCount()} active";

            // Page label
            var mods = MenuState.Mods[MenuState.ActiveCategory];
            int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)mods.Count / ModsPerPage));
            if (_pageText != null)
                _pageText.text = $"{_pageStart / ModsPerPage + 1}/{pageCount}";

            // Rows
            var content = _bg.transform.Find("Content");
            if (content == null) return;

            float y = -26f;
            for (int i = _pageStart; i < mods.Count && i < _pageStart + ModsPerPage; i++)
            {
                int idx = i;
                ModDef def = mods[i];

                bool even = (i % 2 == 0);
                var row = CreatePanel("Row_" + i, content, even ? Color.clear : RowAltColor,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, 1f), new Vector2(0f, y));
                row.GetComponent<RectTransform>().sizeDelta = new Vector2(-2f, RowHeight);

                // Label
                string prefix = def.Type == ModType.Toggle ? (def.Enabled ? "[x] " : "[ ] ") : "> ";
                var label = CreateText(row.transform, prefix + def.Name, 7,
                    FontStyle.Normal, TextAnchor.MiddleLeft,
                    def.Enabled ? Accent : TextPrimary,
                    new Vector2(0f, 0f), new Vector2(0.72f, 1f), new Vector2(3f, 0f));
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (!string.IsNullOrEmpty(def.Tooltip))
                    label.text += $"  <size=6><color=#888888>{def.Tooltip}</color></size>";

                // Value readout for sliders
                if (def.Type == ModType.Slider)
                {
                    var valueText = CreateText(row.transform,
                        Mathf.RoundToInt(Mathf.Lerp(def.Min, def.Max, def.Value)).ToString(), 7,
                        FontStyle.Normal, TextAnchor.MiddleRight, TextSecondary,
                        new Vector2(0.72f, 0f), new Vector2(1f, 0f), Vector2.zero);
                    valueText.alignment = TextAnchor.MiddleRight;
                    CreateText(row.transform, "  <", 7,
                        FontStyle.Normal, TextAnchor.MiddleRight, TextSecondary,
                        new Vector2(0.72f, 0f), new Vector2(1f, 0f), new Vector2(-6f, 0f));
                }

                // Button
                var btn = row.AddComponent<Button>();
                var cb  = btn.colors;
                cb.normalColor      = Color.clear;
                cb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
                cb.pressedColor     = new Color(1f, 1f, 1f, 0.14f);
                btn.colors = cb;
                btn.onClick.AddListener(() => OnModPressed(idx));

                _modRows.Add(row);
                y -= RowHeight;
            }
        }

        // ── Mod interaction ───────────────────────────────────────────
        private void OnModPressed(int index)
        {
            var def = MenuState.Mods[MenuState.ActiveCategory][index];
            switch (def.Type)
            {
                case ModType.Toggle:
                    def.Enabled = !def.Enabled;
                    MenuState.Mods[MenuState.ActiveCategory][index] = def;
                    Mods.OnModToggled(MenuState.ActiveCategory, index, def.Enabled);
                    break;

                case ModType.Button:
                    Mods.OnModActivated(MenuState.ActiveCategory, index);
                    break;

                case ModType.Slider:
                    // step slider up by 10%
                    def.Value = Mathf.Min(1f, def.Value + 0.1f);
                    MenuState.Mods[MenuState.ActiveCategory][index] = def;
                    Mods.OnModChanged(MenuState.ActiveCategory, index, def.Value);
                    break;
            }
            RebuildContent();
        }

        // ── Update (anchor the panel to the wrist) ────────────────────
        private void Update()
        {
            if (_canvasGO == null || _anchor == null) return;

            // Nudge out of the hand mesh and face the player.
            Vector3 offset = _anchor.forward * 0.06f + _anchor.up * 0.05f;
            _canvasGO.transform.position = _anchor.position + offset;
            _canvasGO.transform.rotation = Quaternion.LookRotation(_anchor.up, -_anchor.forward);
        }

        // ── UI helpers ────────────────────────────────────────────────
        private GameObject CreatePanel(string name, Transform parent, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin       = anchorMin;
            rt.anchorMax       = anchorMax;
            rt.pivot           = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.offsetMin       = Vector2.zero;
            rt.offsetMax       = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = true;
            if (_uiMat != null) img.material = _uiMat;
            return go;
        }

        private Text CreateText(Transform parent, string content, int fontSize,
            FontStyle style, TextAnchor alignment, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.offsetMin        = Vector2.zero;
            rt.offsetMax        = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text               = content;
            t.font               = Font.CreateDynamicFontFromOSFont("Segoe UI", fontSize);
            t.fontSize           = fontSize;
            t.fontStyle          = style;
            t.alignment          = alignment;
            t.color              = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            t.raycastTarget      = false;
            return t;
        }

        private Material GetUIMaterial()
        {
            var shader = Shader.Find("UI/Default") ?? Shader.Find("Unlit/Color");
            return shader != null ? new Material(shader) : null;
        }
    }
}