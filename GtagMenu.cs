using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace GtagMenu
{
    [BepInPlugin("com.gtag.menu", "GtagMenu", "1.0.0")]
    public class WristMenu : BaseUnityPlugin
    {
        // ── Singleton ────────────────────────────────────────────────
        private static WristMenu _instance;
        public static WristMenu Instance
        {
            get => _instance;
            private set => _instance = value;
        }

        // ── Theme colours ─────────────────────────────────────────────
        public static readonly string[] Themes = { "Blood Red", "Cyber Blue", "Neon Green", "Purple Haze" };
        public static int ActiveThemeIndex = 0;

        // ── Category / mod lists ──────────────────────────────────────
        public static readonly string[] Categories = { "Player", "Other Player", "Items", "Fun Mods", "World", "Extra", "Settings" };
        public static int ActiveCategory = 0;

        public static readonly string[][] Toggles = new string[][]
        {
            // Player
            new[]{ "Speed Boost","Jump Power","No Clip","Long Arms","Invisible","God Mode","Low Gravity","High Gravity","Fly","Anti AFK" },
            // Other Player
            new[]{ "Player ESP","Name Tags","Tracers","Distance ESP","Skeleton ESP","Snap Lines","Highlight Players","Radar" },
            // Items
            new[]{ "Item ESP","Auto Pickup","Grab Range","Infinite Ammo","No Recoil","No Spread","Rapid Fire" },
            // Fun Mods
            new[]{ "Spin","Dance","Ragdoll","Explode","Confetti","Bounce","Size Changer" },
            // World
            new[]{ "Full Bright","Third Person","Freecam","Time Scale","Weather Control","No Fog","Sky Changer" },
            // Extra
            new[]{ "FPS Counter","Ping Display","Crosshair","Watermark","Performance Stats" },
            // Settings
            new[]{ "Theme Picker","Save Config","Load Config","Reset All","Menu Sounds" },
        };

        public static bool[] toggles = new bool[100];

        // ── Constants ─────────────────────────────────────────────────
        private const int   ModsPerPage   = 8;
        private const float PaddingPx     = 2f;
        private const float SidebarPct    = 0.27f;
        private const float SphereRadius  = 0.014f;
        private const float RowAlt        = 14f;

        // Theme colour slots (set by ActiveThemeIndex)
        private Color Accent       => ThemeAccent(ActiveThemeIndex);
        private Color TextPrimary  => Color.white;
        private Color TextSecondary=> new Color(0.85f, 0.85f, 0.85f, 0.9f);
        private Color RowAltColor  => new Color(0f, 0f, 0f, 0.15f);

        private static Color ThemeAccent(int idx)
        {
            switch (idx)
            {
                case 0:  return new Color(0.8f, 0.1f, 0.1f, 1f);  // Blood Red
                case 1:  return new Color(0.1f, 0.4f, 0.9f, 1f);  // Cyber Blue
                case 2:  return new Color(0.1f, 0.8f, 0.2f, 1f);  // Neon Green
                default: return new Color(0.5f, 0.1f, 0.8f, 1f);  // Purple Haze
            }
        }

        // ── Scene objects ─────────────────────────────────────────────
        private GameObject _canvas;        // WorldSpace canvas root
        private Transform  _holder;        // wrist/hand anchor transform
        private GameObject _sphere;        // click-to-toggle orb GO
        private Renderer   _sphereRend;    // sphere renderer

        private GameObject _bg;            // root BG panel
        private Text       _titleText;
        private Text       _countText;
        private Text       _pageText;
        private List<GameObject> _sidebarBtns = new List<GameObject>();
        private List<GameObject> _modRows     = new List<GameObject>();
        private Material   _uiMat;

        // ── XR device handles ─────────────────────────────────────────
        private InputDevice _leftXRDevice;
        private InputDevice _rightXRDevice;
        private bool        _devicesCached;

        // ── State ─────────────────────────────────────────────────────
        private bool  _ready;
        private bool  _visible;
        private bool  _lastVRYHeld;
        private bool  _lastGrip;
        private float _lastClickTime;
        private int   _frameCount;
        private float _logTimer;
        private int   _pageStart;

        // ── Logger ────────────────────────────────────────────────────
        private static ManualLogSource Log => Instance?.Logger;

        // ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance = this;
            Logger.LogInfo("GtagMenu v1.0.0 loaded — press Y to toggle wrist menu");

            // Create the holder GameObject that will be parented to the wrist
            var holderGO = new GameObject("GtagMenu_Holder");
            DontDestroyOnLoad(holderGO);
            _holder = holderGO.transform;
        }

        private void OnDestroy()
        {
            if (_canvas  != null) Destroy(_canvas);
            if (_sphere  != null) Destroy(_sphere);
            if (_bg      != null) Destroy(_bg);
        }

        // ─────────────────────────────────────────────────────────────
        private void Update()
        {
            _logTimer++;

            // ── Wait for rig ──────────────────────────────────────────
            if (!_ready)
            {
                try
                {
                    var rig = GorillaTagger.Instance?.offlineVRRig;
                    if (rig == null) return;

                    _holder     = rig.leftHandTransform;
                    _sphereRend = rig.rightHandTransform;   // sphere anchor = right wrist
                }
                catch { return; }

                if (_holder == null || _sphereRend == null) return;

                _ready = true;
                Logger.LogInfo("[WristMenu] Rig found — building menu");
                RebuildContent();
                HandleClick();
                BuildCanvas();
                return;
            }

            // ── Cache XR devices once ─────────────────────────────────
            if (!_devicesCached)
                CacheXRDevices();

            // ── Toggle via left secondary (Y) ─────────────────────────
            bool cipBtn = false;
            try
            {
                cipBtn = ControllerInputPoller.instance.leftControllerSecondaryButton;
                if (cipBtn) Logger.LogInfo("[Input] CIP.leftSecondary=TRUE");
            }
            catch { }

            bool xrBtn = false;
            if (_leftXRDevice.isValid)
            {
                _leftXRDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out xrBtn);
                if (xrBtn) Logger.LogInfo("[Input] XR.secondaryButton=TRUE");
            }
            else if (_devicesCached)
            {
                // try reacquire
                _devicesCached = false;
            }

            bool menuBtn = false;
            if (_leftXRDevice.isValid)
                _leftXRDevice.TryGetFeatureValue(CommonUsages.menuButton, out menuBtn);
            if (menuBtn) Logger.LogInfo("[Input] XR.menuButton=TRUE");

            bool yKey = false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null) yKey = kb.yKey.wasPressedThisFrame;
            if (yKey) Logger.LogInfo("[Input] Keyboard Y (InputSystem)");

            bool toggle = cipBtn || xrBtn || menuBtn || yKey;

            // Periodic log
            if (_frameCount++ >= 300)
            {
                _frameCount = 0;
                Logger.LogInfo(string.Format("[Input] periodic: yHeld={0} visible={1} cip={2} cipBtn={3} frames={4}",
                    _lastVRYHeld, _visible, false, cipBtn, _frameCount));
            }

            if (toggle && !_lastVRYHeld)
            {
                _visible    = !_visible;
                _lastVRYHeld = true;

                // Show/hide by scale trick (original behaviour)
                float scale = _visible ? 0.0007f : 9999f;
                _canvas?.transform.GetComponent<CanvasScaler>()?.transform.localScale
                    .Equals(new Vector3(scale, scale, scale));

                if (_canvas != null)
                    _canvas.transform.localScale = _visible
                        ? new Vector3(0.0007f, 0.0007f, 0.0007f)
                        : new Vector3(9999f,  9999f,  9999f);

                if (_sphere != null)
                    _sphere.transform.localScale = _visible
                        ? new Vector3(0.0007f, 0.0007f, 0.0007f)
                        : new Vector3(9999f,  9999f,  9999f);
            }
            else if (!toggle)
            {
                _lastVRYHeld = false;
            }

            if (!_visible) return;

            PositionMenu();
            PositionSphere();
        }

        // ─────────────────────────────────────────────────────────────
        private void CacheXRDevices()
        {
            try
            {
                var leftList  = new List<InputDevice>();
                var rightList = new List<InputDevice>();
                InputDevices.GetDevicesAtXRNode(XRNode.LeftHand,  leftList);
                InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightList);

                if (leftList.Count  > 0) _leftXRDevice  = leftList[0];
                if (rightList.Count > 0) _rightXRDevice = rightList[0];

                _devicesCached = true;
                Logger.LogInfo(string.Format("[WristMenu] Cached XR devices — left={0} right={1}",
                    _leftXRDevice.isValid, _rightXRDevice.isValid));
            }
            catch (Exception e)
            {
                Logger.LogInfo("[WristMenu] CacheXRDevices error: " + e.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            // ── Canvas root ───────────────────────────────────────────
            _canvas = new GameObject("WristMenu");
            DontDestroyOnLoad(_canvas);

            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 999;
            canvas.overrideSorting = true;

            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 0.0007f;

            _canvas.AddComponent<GraphicRaycaster>();

            var rt = _canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(420f, 500f);

            // ── Background panel ──────────────────────────────────────
            _bg = CreatePanel("BG", _canvas.transform, Color.black,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f,  0f), new Vector2(0f, 0f));

            // ── Header ────────────────────────────────────────────────
            var headerPanel = CreatePanel("Header", _bg.transform, new Color(0.1f, 0.1f, 0.1f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -32f));
            headerPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 32f);

            _titleText = CreateText(headerPanel.transform, "Gtag Menu", 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, TextPrimary,
                new Vector2(0f, 0f), new Vector2(0.6f, 1f),
                new Vector2(8f, 0f));

            _countText = CreateText(headerPanel.transform, "0 active", 9, FontStyle.Normal,
                TextAnchor.MiddleRight, TextSecondary,
                new Vector2(0.6f, 0f), new Vector2(1f, 1f),
                new Vector2(-8f, 0f));

            // ── Sidebar ───────────────────────────────────────────────
            var sidebar = CreatePanel("Sidebar", _bg.transform, new Color(0.05f, 0.05f, 0.05f, 1f),
                new Vector2(0f,    0f), new Vector2(SidebarPct, 1f),
                new Vector2(0f,    0f), new Vector2(0f, -32f));

            BuildSidebar(sidebar.transform);

            // ── Content ───────────────────────────────────────────────
            var content = CreatePanel("Content", _bg.transform, new Color(0.08f, 0.08f, 0.08f, 0.78f),
                new Vector2(SidebarPct, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(2f, -32f));

            BuildContent(content.transform);

            // ── Enabled column ────────────────────────────────────────
            var enabledCol = CreatePanel("Enabled", _bg.transform, new Color(0.85f, 0.85f, 0.85f, 0.9f),
                new Vector2(0.77f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(2f, -32f));

            BuildEnabledColumn(enabledCol.transform);

            // ── Click sphere ──────────────────────────────────────────
            _sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere.name = "ClickSphere";
            _sphere.transform.localScale = Vector3.one * SphereRadius;
            _sphereRend = _sphere.GetComponent<Renderer>();
            _uiMat = GetUIMaterial();
            if (_uiMat != null) _sphereRend.material = _uiMat;
            Destroy(_sphere.GetComponent<Collider>());
            DontDestroyOnLoad(_sphere);
        }

        // ─────────────────────────────────────────────────────────────
        private void RebuildContent()
        {
            // Destroy old rows
            foreach (var r in _modRows) if (r) Destroy(r);
            _modRows.Clear();

            // Update sidebar button highlights
            for (int i = 0; i < _sidebarBtns.Count; i++)
            {
                var btn = _sidebarBtns[i]?.GetComponent<Button>();
                if (btn == null) continue;
                var cb = btn.colors;
                cb.normalColor = i == ActiveCategory ? Accent : new Color(0.15f, 0.15f, 0.15f, 1f);
                btn.colors = cb;
            }

            // Page text
            int modCount  = Toggles[ActiveCategory].Length;
            int pageCount = Mathf.CeilToInt((float)modCount / ModsPerPage);
            int page      = _pageStart / ModsPerPage + 1;
            if (_pageText) _pageText.text = string.Format("Page {0}/{1}", page, pageCount);

            // Count active
            int activeCount = 0;
            foreach (bool t in toggles) if (t) activeCount++;
            if (_countText) _countText.text = string.Format("{0} active", activeCount);
        }

        // ─────────────────────────────────────────────────────────────
        private void BuildSidebar(Transform parent)
        {
            _sidebarBtns.Clear();
            float y = -4f;
            for (int i = 0; i < Categories.Length; i++)
            {
                int idx = i;
                var panel = CreatePanel("Cat_" + i, parent, new Color(0.15f, 0.15f, 0.15f, 1f),
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(y));

                var hdr = CreateText(panel.transform, Categories[i], 7, FontStyle.Normal,
                    TextAnchor.MiddleLeft, TextPrimary,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, 0f));

                panel.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 22f);

                var btn = panel.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    ActiveCategory = idx;
                    _pageStart = 0;
                    RebuildContent();
                });

                _sidebarBtns.Add(panel);
                y -= 23f;
            }
        }

        // ─────────────────────────────────────────────────────────────
        private void BuildContent(Transform parent)
        {
            // Page navigation arrows
            var upBtn = CreatePanel("PageUp", parent, new Color(0.2f, 0.2f, 0.2f, 1f),
                new Vector2(0f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -2f));
            upBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 14f);
            var upT = CreateText(upBtn.transform, "▲", 9, FontStyle.Normal,
                TextAnchor.MiddleCenter, TextPrimary, Vector2.zero, Vector2.one, Vector2.zero);
            upBtn.AddComponent<Button>().onClick.AddListener(() =>
            {
                _pageStart = Mathf.Max(0, _pageStart - ModsPerPage);
                RebuildContent();
            });

            _pageText = CreateText(parent, "Page 1/1", 8, FontStyle.Normal,
                TextAnchor.MiddleCenter, TextSecondary,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -2f));

            var dnBtn = CreatePanel("PageDn", parent, new Color(0.2f, 0.2f, 0.2f, 1f),
                new Vector2(0.5f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -2f));
            dnBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 14f);
            CreateText(dnBtn.transform, "▼", 9, FontStyle.Normal,
                TextAnchor.MiddleCenter, TextPrimary, Vector2.zero, Vector2.one, Vector2.zero);
            dnBtn.AddComponent<Button>().onClick.AddListener(() =>
            {
                int max = Mathf.Max(0, Toggles[ActiveCategory].Length - ModsPerPage);
                _pageStart = Mathf.Min(max, _pageStart + ModsPerPage);
                RebuildContent();
            });

            // Mod rows will be added by ModScroll
            ModScroll(parent);
        }

        // ─────────────────────────────────────────────────────────────
        private void BuildEnabledColumn(Transform parent)
        {
            float y = -4f;
            foreach (bool t in toggles)
            {
                if (y < -320f) break;
                var row = CreatePanel("ERow", parent, Color.clear,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, 1f), new Vector2(y));
                row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, RowAlt);
                CreateText(row.transform, t ? "✓" : "", 7, FontStyle.Normal,
                    TextAnchor.MiddleCenter, Accent, Vector2.zero, Vector2.one, Vector2.zero);
                y -= RowAlt;
            }
        }

        // ─────────────────────────────────────────────────────────────
        private void ModScroll(Transform parent)
        {
            float y = -22f;
            string[] mods = Toggles[ActiveCategory];

            for (int i = _pageStart; i < mods.Length && i < _pageStart + ModsPerPage; i++)
            {
                int modIdx = i;
                bool alt = (i % 2) == 1;
                var bg = CreatePanel("TBg_" + i, parent,
                    alt ? RowAltColor : Color.clear,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, 1f), new Vector2(y));
                bg.GetComponent<RectTransform>().sizeDelta = new Vector2(-2f, RowAlt);

                CreateText(bg.transform, "• " + mods[i], 7, FontStyle.Normal,
                    TextAnchor.MiddleLeft, toggles[modIdx] ? Accent : TextPrimary,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(2f, y - 13f));

                var btn = bg.AddComponent<Button>();
                var cb  = btn.colors;
                cb.normalColor      = Color.clear;
                cb.highlightedColor = new Color(1f, 1f, 1f, 0.1f);
                cb.pressedColor     = new Color(1f, 1f, 1f, 0.2f);
                btn.colors = cb;
                btn.onClick.AddListener(() => OnModClick(modIdx));

                _modRows.Add(bg);
                y -= RowAlt;
            }
        }

        // ─────────────────────────────────────────────────────────────
        private void OnModClick(int modIndex)
        {
            float now = Time.time;
            if (now - _lastClickTime < 0.2f) return;
            _lastClickTime = now;
            HandleClick(modIndex);
        }

        private void HandleClick(int modIndex = -1)
        {
            if (modIndex < 0) return;
            toggles[modIndex] = !toggles[modIndex];
            RebuildContent();
        }

        // ─────────────────────────────────────────────────────────────
        // FIXED: was 0.03f/0.04f — too close, clipped into hand mesh
        private void PositionMenu()
        {
            if (_holder == null || _canvas == null) return;

            Vector3 offset = _holder.forward * 0.06f
                           + _holder.up      * 0.05f;

            _canvas.transform.position = _holder.position + offset;
            _canvas.transform.rotation = Quaternion.LookRotation(
                _holder.up,
                -_holder.forward
            );
        }

        private void PositionSphere()
        {
            if (_sphere == null || _sphereRend == null) return;

            var anchor = _sphereRend.transform; // right wrist
            _sphere.transform.position = anchor.position
                + anchor.up      * 0.04f
                + anchor.forward * -0.02f;
            _sphere.transform.rotation = anchor.rotation;
        }

        // ─────────────────────────────────────────────────────────────
        // ── UI helpers ────────────────────────────────────────────────
        private GameObject CreatePanel(string name, Transform parent,
            Color color,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot,     Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin       = anchorMin;
            rt.anchorMax       = anchorMax;
            rt.pivot           = pivot;
            rt.anchoredPosition= anchoredPos;
            rt.offsetMin       = Vector2.zero;
            rt.offsetMax       = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color          = color;
            img.raycastTarget  = true;
            if (_uiMat != null) img.material = _uiMat;
            return go;
        }

        private Text CreateText(Transform parent, string content,
            int fontSize, FontStyle style,
            TextAnchor alignment, Color color,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos)
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
            t.text                = content;
            t.font                = Font.CreateDynamicFontFromOSFont("Segoe UI", fontSize);
            t.fontSize            = fontSize;
            t.fontStyle           = style;
            t.alignment           = alignment;
            t.color               = color;
            t.horizontalOverflow  = HorizontalWrapMode.Overflow;
            t.verticalOverflow    = VerticalWrapMode.Overflow;
            t.raycastTarget       = false;
            return t;
        }

        private Material GetUIMaterial()
        {
            var shader = Shader.Find("UI/Default") ?? Shader.Find("Unlit/Color");
            return shader != null ? new Material(shader) : null;
        }
    }
}
