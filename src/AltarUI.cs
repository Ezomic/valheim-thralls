using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// The altar's ledger. Plain IMGUI so the mod stays a single DLL with no asset bundle.
    ///
    /// Laid out by hand in three bands - header strip, body, footer strip - rather than by
    /// letting GUILayout stack everything. IMGUI's boxes and buttons carry their own heavy
    /// chrome, and a panel built out of them reads as a debug window; the game's own panels
    /// are flat fields separated by hairlines, which is what this draws.
    /// </summary>
    internal static class AltarUI
    {
        private const int WindowId = 0x7B1A11;

        private const float HeaderH = 44f;
        private const float FooterH = 46f;
        private const float Pad = 18f;
        private const float RailW = 208f;
        private const float ColGap = 16f;

        private static ThrallAltar _altar;
        private static Rect _window = new Rect(0f, 0f, 940f, 590f);
        private static Vector2 _scroll;
        private static bool _placed;
        private static readonly Dictionary<Thrall, string> NameBuffers = new Dictionary<Thrall, string>();

        private enum Filter { All, Working, Idle }
        private static Filter _filter = Filter.All;

        /// <summary>
        /// The panel is three screens, not one. The overview is for reading - who you have
        /// and what they are doing - and commanding a thrall or binding a new one happens
        /// on its own screen, where there is room to say something about it first.
        /// </summary>
        private enum View { Overview, Breed, Thrall }

        private static View _view = View.Overview;
        private static int _breed = 1;
        private static Thrall _subject;

        public static bool IsOpen { get; private set; }

        public static void Toggle(ThrallAltar altar)
        {
            if (IsOpen) { Close(); return; }
            _altar = altar;
            IsOpen = true;
            NameBuffers.Clear();
        }

        public static void Close()
        {
            IsOpen = false;
            _altar = null;
            NameBuffers.Clear();
        }

        /// <summary>Shuts the panel when you walk away from the altar.</summary>
        public static void Tick()
        {
            if (!IsOpen) return;

            if (_altar == null || Player.m_localPlayer == null)
            {
                Close();
                return;
            }

            if (Vector3.Distance(Player.m_localPlayer.transform.position, _altar.transform.position) > 8f)
                Close();
        }

        public static void Draw()
        {
            if (!IsOpen) return;

            EnsureStyles();

            if (!_placed)
            {
                _window.x = (Screen.width - _window.width) * 0.5f;
                _window.y = (Screen.height - _window.height) * 0.5f;
                _placed = true;
            }

            var oldSkin = GUI.skin;
            GUI.skin = Skin();
            GUI.backgroundColor = Color.white;

            _window = GUI.Window(WindowId, _window, DrawWindow, "");

            GUI.skin = oldSkin;
        }

        // ------------------------------------------------------------- palette

        private static readonly Color Panel = new Color(0.086f, 0.070f, 0.055f, 1f);
        private static readonly Color Strip = new Color(0.145f, 0.113f, 0.082f, 1f);
        private static readonly Color Edge = new Color(0.420f, 0.337f, 0.227f, 1f);
        private static readonly Color Hair = new Color(0.290f, 0.239f, 0.173f, 1f);
        private static readonly Color Parchment = new Color(0.960f, 0.925f, 0.845f);
        private static readonly Color Brass = new Color(0.94f, 0.80f, 0.45f);
        private static readonly Color Faded = new Color(0.780f, 0.705f, 0.560f);
        private static readonly Color Live = new Color(0.855f, 0.925f, 0.680f);
        private static readonly Color Locked = new Color(0.660f, 0.590f, 0.480f);
        private static readonly Color CardOn = new Color(0.200f, 0.250f, 0.145f, 1f);
        private static readonly Color CardOff = new Color(0.175f, 0.140f, 0.105f, 1f);
        private static readonly Color ChipOn = new Color(0.184f, 0.227f, 0.133f, 1f);
        private static readonly Color Pill = new Color(0.227f, 0.184f, 0.133f, 1f);

        private static GUISkin _skin;
        private static Texture2D _panelTex, _stripTex, _hairTex, _edgeTex;
        private static Texture2D _fieldTex, _chipTex, _chipOnTex, _cardOnTex, _cardOffTex, _pillTex;

        private static GUIStyle _titleStyle, _metaStyle, _sectionStyle, _rowNameStyle;
        private static GUIStyle _mutedStyle, _liveStyle, _chipStyle, _pillStyle;
        private static GUIStyle _cardNameStyle, _cardMetaStyle, _cardLockedStyle;
        private static GUIStyle _nameStyle, _footStyle, _disabledStyle, _disabledChip;

        private static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private static GUISkin Skin()
        {
            if (_skin != null) return _skin;

            _panelTex = Solid(Panel);
            _stripTex = Solid(Strip);
            _hairTex = Solid(Hair);
            _edgeTex = Solid(Edge);
            _fieldTex = Solid(new Color(0.055f, 0.045f, 0.035f, 1f));
            _chipTex = Solid(new Color(0.20f, 0.165f, 0.125f, 1f));
            _chipOnTex = Solid(ChipOn);
            _cardOnTex = Solid(CardOn);
            _cardOffTex = Solid(CardOff);
            _pillTex = Solid(Pill);

            _skin = Object.Instantiate(GUI.skin);
            _skin.hideFlags = HideFlags.HideAndDontSave;

            var font = GameFont();
            if (font != null) _skin.font = font;

            _skin.window.normal.background = _panelTex;
            _skin.window.onNormal.background = _panelTex;
            _skin.window.border = new RectOffset(0, 0, 0, 0);
            // No padding: the three bands are placed by hand, edge to edge.
            _skin.window.padding = new RectOffset(0, 0, 0, 0);

            _skin.label.fontSize = 14;
            _skin.label.normal.textColor = Parchment;
            _skin.label.padding = new RectOffset(0, 0, 2, 2);
            _skin.label.margin = new RectOffset(0, 0, 0, 0);

            _skin.button.fontSize = 13;
            _skin.button.normal.background = _chipTex;
            _skin.button.normal.textColor = Parchment;
            _skin.button.hover.background = _chipTex;
            _skin.button.hover.textColor = Brass;
            _skin.button.active.background = _chipOnTex;
            _skin.button.active.textColor = Brass;
            _skin.button.border = new RectOffset(0, 0, 0, 0);
            _skin.button.padding = new RectOffset(6, 6, 4, 4);
            _skin.button.margin = new RectOffset(0, 5, 0, 0);

            _skin.textField.fontSize = 14;
            _skin.textField.normal.background = _fieldTex;
            _skin.textField.normal.textColor = Parchment;
            _skin.textField.border = new RectOffset(0, 0, 0, 0);
            _skin.textField.padding = new RectOffset(7, 7, 4, 4);
            _skin.textField.margin = new RectOffset(0, 0, 0, 0);

            _skin.verticalScrollbar.fixedWidth = 10f;
            _skin.horizontalScrollbar.fixedHeight = 0f;

            return _skin;
        }

        private static Font GameFont()
        {
            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            Font fallback = null;

            for (int i = 0; i < fonts.Length; i++)
            {
                var name = fonts[i] != null ? fonts[i].name : "";
                if (name.IndexOf("Averia", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Norse", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return fonts[i];

                if (fallback == null && fonts[i] != null) fallback = fonts[i];
            }
            return fallback;
        }

        private static void EnsureStyles()
        {
            if (_titleStyle != null) return;
            var skin = Skin();

            _titleStyle = new GUIStyle(skin.label)
            {
                fontSize = 20,
                normal = { textColor = Brass },
                alignment = TextAnchor.MiddleLeft
            };

            _metaStyle = new GUIStyle(skin.label)
            {
                fontSize = 12,
                normal = { textColor = Faded },
                alignment = TextAnchor.MiddleRight
            };

            _sectionStyle = new GUIStyle(skin.label)
            {
                fontSize = 12,
                normal = { textColor = Faded }
            };

            _rowNameStyle = new GUIStyle(skin.label) { fontSize = 14 };
            _mutedStyle = new GUIStyle(skin.label) { fontSize = 12, normal = { textColor = Faded } };
            _liveStyle = new GUIStyle(skin.label) { fontSize = 12, normal = { textColor = Live } };

            _chipStyle = new GUIStyle(skin.button)
            {
                fontSize = 12,
                padding = new RectOffset(0, 0, 3, 3),
                margin = new RectOffset(0, 5, 0, 0),
                alignment = TextAnchor.MiddleCenter
            };

            _pillStyle = new GUIStyle(skin.button)
            {
                fontSize = 12,
                padding = new RectOffset(0, 0, 4, 4),
                margin = new RectOffset(0, 6, 0, 0)
            };

            _cardNameStyle = new GUIStyle(skin.label)
            {
                fontSize = 14,
                normal = { textColor = Live },
                padding = new RectOffset(9, 9, 6, 0)
            };
            _cardMetaStyle = new GUIStyle(skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.561f, 0.627f, 0.416f) },
                padding = new RectOffset(9, 9, 0, 6)
            };
            _cardLockedStyle = new GUIStyle(skin.label)
            {
                fontSize = 12,
                normal = { textColor = Locked },
                padding = new RectOffset(9, 9, 0, 6)
            };

            _nameStyle = new GUIStyle(skin.textField) { fontSize = 14 };
            _footStyle = new GUIStyle(skin.button) { fontSize = 13, padding = new RectOffset(0, 0, 5, 5) };

            _disabledChip = new GUIStyle(_chipStyle)
            {
                normal = { background = _chipTex, textColor = Locked },
                hover = { background = _chipTex, textColor = Locked },
                active = { background = _chipTex, textColor = Locked }
            };

            // Visibly dead: same shape, no highlight on hover, greyed text.
            _disabledStyle = new GUIStyle(_footStyle)
            {
                normal = { background = _chipTex, textColor = Locked },
                hover = { background = _chipTex, textColor = Locked },
                active = { background = _chipTex, textColor = Locked }
            };
        }

        // ------------------------------------------------------------- drawing helpers

        private static void Rule(Rect where, Texture2D tex)
        {
            GUI.DrawTexture(where, tex);
        }

        /// <summary>A hairline across the current layout position.</summary>
        private static void HairLine(float width)
        {
            var r = GUILayoutUtility.GetRect(width, 1f, GUILayout.Height(1f));
            GUI.DrawTexture(r, _hairTex);
        }

        // ------------------------------------------------------------- window

        private static void DrawWindow(int id)
        {
            var w = _window.width;
            var h = _window.height;

            // The three bands.
            GUI.DrawTexture(new Rect(0f, 0f, w, HeaderH), _stripTex);
            Rule(new Rect(0f, HeaderH, w, 1f), _edgeTex);

            GUI.DrawTexture(new Rect(0f, h - FooterH, w, FooterH), _stripTex);
            Rule(new Rect(0f, h - FooterH - 1f, w, 1f), _edgeTex);

            DrawHeader(w);

            var bodyY = HeaderH + 1f;
            var bodyH = h - FooterH - bodyY - 1f;

            if (_view != View.Overview)
            {
                GUILayout.BeginArea(new Rect(Pad, bodyY + 10f, w - Pad * 2f, bodyH - 20f));
                if (_view == View.Breed) DrawBreedPage(w - Pad * 2f);
                else DrawThrallPage(w - Pad * 2f);
                GUILayout.EndArea();

                GUILayout.BeginArea(new Rect(Pad, h - FooterH + 9f, w - Pad * 2f, FooterH - 18f));
                DrawFooter();
                GUILayout.EndArea();

                GUI.DragWindow(new Rect(0f, 0f, w, HeaderH));
                return;
            }

            // The divider between the two columns, drawn full height of the body.
            Rule(new Rect(Pad + RailW + ColGap * 0.5f, bodyY + 6f, 1f, bodyH - 12f), _hairTex);

            GUILayout.BeginArea(new Rect(Pad, bodyY + 10f, RailW, bodyH - 20f));
            DrawSummonRail();
            GUILayout.EndArea();

            var rosterX = Pad + RailW + ColGap;
            GUILayout.BeginArea(new Rect(rosterX, bodyY + 10f, w - rosterX - Pad, bodyH - 20f));
            DrawRoster(w - rosterX - Pad);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(Pad, h - FooterH + 9f, w - Pad * 2f, FooterH - 18f));
            DrawFooter();
            GUILayout.EndArea();

            GUI.DragWindow(new Rect(0f, 0f, w, HeaderH));
        }

        private static void DrawHeader(float w)
        {
            GUILayout.BeginArea(new Rect(Pad, 0f, w - Pad * 2f, HeaderH));
            GUILayout.BeginHorizontal();
            GUILayout.Space(0f);

            GUILayout.Label(_altar != null ? _altar.GetHoverName() : "Summoning altar",
                _titleStyle, GUILayout.Height(HeaderH));

            GUILayout.FlexibleSpace();

            GUILayout.Label(string.Format("{0} of {1} at work   ·   {2} of {3} bound",
                    ThrallRegistry.WorkingCount(), ThrallAltar.Slots,
                    ThrallRegistry.Count(), ThrallConfig.MaxThralls.Value),
                _metaStyle, GUILayout.Height(HeaderH));

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------- left rail

        private static void DrawSummonRail()
        {
            GUILayout.Label("Raise", _sectionStyle);
            GUILayout.Space(4f);

            var player = Player.m_localPlayer;
            var price = Mathf.Max(0, ThrallConfig.HeadsPerWorker.Value);

            for (int tier = 1; tier <= ThrallBreed.Count; tier++)
            {
                var unlocked = ThrallBreed.Unlocked(tier);
                var have = player != null ? Trophies.Count(player.GetInventory(), tier) : 0;
                var affordable = unlocked && (price == 0 || have >= price);

                var cost = ThrallBreed.RaiseCost(tier);
                var paid = player != null && (string.IsNullOrEmpty(cost)
                                              || ItemCost.CanPay(player.GetInventory(), cost));

                affordable = affordable && paid;

                var detail = !unlocked
                    ? ThrallBreed.Blocker(tier)
                    : !paid ? "not enough materials"
                    : price == 0 ? "ready" : Mathf.Min(have, price) + " of " + price + " heads";

                TierCard(tier, ThrallBreed.NameFor(tier), detail, unlocked, affordable);
                GUILayout.Space(6f);
            }

            DrawUpgrade();

            // The ones you sent away, waiting to be called back as themselves.
            if (Resting.Count > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Resting", _sectionStyle);
                GUILayout.Space(4f);

                var resting = new List<RestingThrall>(Resting.All);
                foreach (var entry in resting)
                {
                    var card = GUILayoutUtility.GetRect(RailW, 44f, GUILayout.Height(44f));
                    GUI.DrawTexture(card, _cardOffTex);
                    Outline(card, Hair);

                    GUI.Label(new Rect(card.x, card.y, card.width, 20f),
                        entry.Name, _cardNameStyle);
                    GUI.Label(new Rect(card.x, card.y + 17f, card.width, 18f),
                        entry.TierName + " lv" + entry.Level, _cardLockedStyle);

                    if (GUI.Button(new Rect(card.xMax - 78f, card.y + 22f, 68f, 16f),
                            "Call back", _chipStyle) && _altar != null)
                    {
                        ThrallsPlugin.Recall(entry, _altar.SummonSpot());
                    }

                    GUILayout.Space(6f);
                }
            }

            if (Fallen.Count == 0) return;

            GUILayout.Space(8f);
            GUILayout.Label("Fallen", _sectionStyle);
            GUILayout.Space(4f);

            var roll = new List<FallenThrall>(Fallen.All);
            foreach (var entry in roll)
            {
                var cost = ThrallBreed.ResurrectCost(entry.Tier);
                var affordable = player != null && ItemCost.CanPay(player.GetInventory(), cost);

                var card = GUILayoutUtility.GetRect(RailW, 54f, GUILayout.Height(54f));
                GUI.DrawTexture(card, affordable ? _cardOnTex : _cardOffTex);
                Outline(card, affordable ? new Color(0.49f, 0.56f, 0.35f) : Hair);

                GUI.Label(new Rect(card.x, card.y, card.width, 20f),
                    entry.Name + "  ·  " + entry.TierName + " lv" + entry.Level, _cardNameStyle);
                GUI.Label(new Rect(card.x, card.y + 18f, card.width, 18f),
                    ItemCost.Describe(cost), affordable ? _cardMetaStyle : _cardLockedStyle);

                if (GUI.Button(new Rect(card.x + 9f, card.y + 34f, 84f, 16f), "Bring back", _chipStyle)
                    && _altar != null)
                {
                    ThrallsPlugin.Resurrect(entry, _altar.SummonSpot());
                }

                GUILayout.Space(6f);
            }
        }

        /// <summary>
        /// The altar's own three upgrades. Each one opens the next breed, so the rail
        /// reads top to bottom as what you have and what building work stands between you
        /// and the rest.
        /// </summary>
        private static void DrawUpgrade()
        {
            GUILayout.Space(10f);
            GUILayout.Label("The altar", _sectionStyle);
            GUILayout.Space(4f);

            var level = _altar != null ? _altar.Upgrades : 0;

            if (level >= 3)
            {
                GUILayout.Label("Finished. All four answer it.", _mutedStyle);
                GUILayout.Space(4f);
                return;
            }

            // Nothing to press: the upgrades are pieces you build beside the altar with
            // the hammer, the way the game gates its own stations.
            var card = GUILayoutUtility.GetRect(RailW, 66f, GUILayout.Height(66f));

            GUI.DrawTexture(card, _cardOffTex);
            Outline(card, new Color(0.353f, 0.298f, 0.220f));

            GUI.Label(new Rect(card.x, card.y, card.width, 20f),
                ThrallConfig.UpgradeName(level + 1), LockedName());

            var wrapped = new GUIStyle(_cardLockedStyle) { wordWrap = true };
            GUI.Label(new Rect(card.x, card.y + 19f, card.width, 44f),
                "Build it beside the altar with your hammer.", wrapped);

            GUILayout.Space(6f);
        }

        private static void TierCard(int tier, string name, string detail, bool unlocked, bool affordable)
        {
            var card = GUILayoutUtility.GetRect(RailW, 46f, GUILayout.Height(46f));

            GUI.DrawTexture(card, unlocked ? _cardOnTex : _cardOffTex);
            Outline(card, unlocked ? new Color(0.49f, 0.56f, 0.35f) : new Color(0.353f, 0.298f, 0.220f));

            GUI.Label(new Rect(card.x, card.y, card.width, 22f), name,
                unlocked ? _cardNameStyle : LockedName());

            GUI.Label(new Rect(card.x, card.y + 21f, card.width, 20f), detail,
                unlocked ? _cardMetaStyle : _cardLockedStyle);

            // The card opens the breed's own page rather than binding on the spot. A
            // single click that spends ten heads with no chance to read what you are
            // getting is a click people regret.
            if (GUI.Button(card, GUIContent.none, GUIStyle.none))
            {
                _breed = tier;
                _view = View.Breed;
            }
        }

        // ------------------------------------------------------------- breed page

        private static void DrawBreedPage(float width)
        {
            var tier = ThrallBreed.Clamp(_breed);
            var unlocked = ThrallBreed.Unlocked(tier);

            if (GUILayout.Button("< Back", _chipStyle, GUILayout.Width(70f), GUILayout.Height(22f)))
                _view = View.Overview;

            GUILayout.Space(10f);
            GUILayout.Label(ThrallBreed.NameFor(tier), _titleStyle);
            GUILayout.Space(2f);

            var lore = new GUIStyle(_mutedStyle) { fontSize = 13, wordWrap = true };
            GUILayout.Label(ThrallBreed.Lore(tier), lore, GUILayout.Width(width * 0.62f));

            GUILayout.Space(12f);
            HairLine(width);
            GUILayout.Space(10f);

            var fresh = WorkPower.For(tier, 1);
            var veteran = WorkPower.For(tier, Levels.MaxLevel);

            Stat("Pack", ThrallBreed.PackSlots(tier, 1) + " slots, "
                         + ThrallBreed.PackSlots(tier, Levels.MaxLevel) + " at level " + Levels.MaxLevel);
            Stat("Chopping", fresh.Chop.ToString("0.#") + " → " + veteran.Chop.ToString("0.#") + " per swing");
            Stat("Mining", fresh.Pickaxe.ToString("0.#") + " → " + veteran.Pickaxe.ToString("0.#") + " per swing");
            Stat("Tool grade", fresh.ToolTier.ToString());
            Stat("Reach", "+" + ThrallBreed.ReachBonus(tier).ToString("0.#") + "m");
            Stat("Raising the dead", ItemCost.Describe(ThrallBreed.ResurrectCost(tier)));

            GUILayout.Space(14f);

            var player = Player.m_localPlayer;
            var price = Mathf.Max(0, ThrallConfig.HeadsPerWorker.Value);
            var have = player != null ? Trophies.Count(player.GetInventory(), tier) : 0;
            var affordable = unlocked && (price == 0 || have >= price);

            if (!unlocked)
            {
                GUILayout.Label(ThrallBreed.Blocker(tier) + " before this one will answer.",
                    _mutedStyle);
            }
            else
            {
                var cost = ThrallBreed.RaiseCost(tier);
                var pack = player != null ? player.GetInventory() : null;
                var paid = pack != null && (string.IsNullOrEmpty(cost) || ItemCost.CanPay(pack, cost));

                GUILayout.Label("Cost", _sectionStyle);
                GUILayout.Space(3f);
                GUILayout.Label(ItemCost.Describe(cost), paid ? _liveStyle : _mutedStyle);

                if (!paid && pack != null)
                {
                    GUILayout.Space(2f);
                    GUILayout.Label("Missing " + ItemCost.Missing(pack, cost), _mutedStyle);
                }

                GUILayout.Space(10f);

                var ready = paid && affordable;

                var previous = GUI.backgroundColor;
                GUI.backgroundColor = ready
                    ? new Color(0.45f, 0.62f, 0.35f)
                    : new Color(0.22f, 0.20f, 0.17f);

                // Drawn but not wired when you cannot pay: a button that looks pressable
                // and then refuses is worse than one that plainly is not.
                var label = price == 0 ? "Raise one"
                    : "Raise one  (" + Mathf.Min(have, price) + " of " + price + " heads)";

                var rect = GUILayoutUtility.GetRect(240f, 30f,
                    GUILayout.Width(240f), GUILayout.Height(30f));

                var face = ready ? _footStyle : _disabledStyle;

                if (GUI.Button(rect, label, face) && ready && _altar != null)
                {
                    ThrallsPlugin.Hire(tier, _altar.SummonSpot());
                    _view = View.Overview;
                }

                GUI.backgroundColor = previous;
            }
        }

        // ------------------------------------------------------------- thrall page

        private static void DrawThrallPage(float width)
        {
            var thrall = _subject;

            if (thrall == null)
            {
                _view = View.Overview;
                return;
            }

            if (GUILayout.Button("< Back", _chipStyle, GUILayout.Width(70f), GUILayout.Height(22f)))
                _view = View.Overview;

            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            string buffer;
            if (!NameBuffers.TryGetValue(thrall, out buffer)) buffer = thrall.ThrallName;

            var edited = GUILayout.TextField(buffer, 20, _nameStyle,
                GUILayout.Width(200f), GUILayout.Height(28f));
            NameBuffers[thrall] = edited;

            if (edited.Trim() != thrall.ThrallName && edited.Trim().Length > 0
                && GUILayout.Button("Rename", _chipStyle, GUILayout.Width(70f), GUILayout.Height(28f)))
            {
                thrall.Rename(edited);
                NameBuffers[thrall] = thrall.ThrallName;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label(thrall.TierName + ", level " + thrall.Rank, _mutedStyle);

            GUILayout.Space(12f);
            HairLine(width);
            GUILayout.Space(10f);

            var power = WorkPower.For(thrall.Tier, thrall.Rank);
            var carried = thrall.Carrying.NrOfItems();
            var slots = thrall.Carrying.GetWidth() * thrall.Carrying.GetHeight();
            var distance = Player.m_localPlayer != null
                ? Mathf.RoundToInt(Vector3.Distance(Player.m_localPlayer.transform.position,
                    thrall.transform.position))
                : 0;

            Stat("Doing", thrall.Hauling ? "hauling to the chest" : WorkNode.JobName(thrall.Job));
            Stat("Pack", carried + " of " + slots + " slots");
            Stat("Experience", thrall.XpProgress);
            Stat("Chopping", power.Chop.ToString("0.#") + " per swing");
            Stat("Mining", power.Pickaxe.ToString("0.#") + " per swing");
            Stat("Drop-off", thrall.HasDropOff ? "set" : "none - it will claim the nearest chest");
            Stat("Distance", distance + "m away");

            GUILayout.Space(14f);
            DrawToolBench(thrall);

            GUILayout.Space(14f);
            GUILayout.Label("Put it to work", _sectionStyle);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            JobChip(thrall, ThrallJob.None, "Idle");
            JobChip(thrall, ThrallJob.Chop, "Chop");
            JobChip(thrall, ThrallJob.Mine, "Mine");
            JobChip(thrall, ThrallJob.Gather, "Gather");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);

            GUILayout.BeginHorizontal();
            JobChip(thrall, ThrallJob.Farm, "Farm");
            JobChip(thrall, ThrallJob.Repair, "Repair");
            JobChip(thrall, ThrallJob.Build, "Build");
            JobChip(thrall, ThrallJob.Follow, "Follow");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(14f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Come here", _footStyle, GUILayout.Width(110f), GUILayout.Height(26f))
                && _altar != null)
            {
                thrall.SummonTo(_altar.SummonSpot());
            }

            GUILayout.Space(8f);

            if (GUILayout.Button("Dismiss", _footStyle, GUILayout.Width(96f), GUILayout.Height(26f)))
            {
                // Kept, not released: it goes onto the resting roll and can be called back
                // with its name, level and tool intact.
                thrall.Dismiss();
                _view = View.Overview;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Hand it a tool, or take one back.
        ///
        /// Only tools you are actually carrying are offered, and handing one over really
        /// removes it from your inventory - otherwise the requirement would be a formality
        /// rather than a cost.
        /// </summary>
        private static void DrawToolBench(Thrall thrall)
        {
            GUILayout.Label("Its tool", _sectionStyle);
            GUILayout.Space(4f);

            var player = Player.m_localPlayer;
            var pack = player != null ? player.GetInventory() : null;

            GUILayout.BeginHorizontal();

            if (thrall.Tool.Length == 0)
            {
                GUILayout.Label("empty handed", _mutedStyle, GUILayout.Width(210f));
            }
            else
            {
                GUILayout.Label(PrettyItem(thrall.Tool), _liveStyle, GUILayout.Width(210f));

                if (GUILayout.Button("Take back", _chipStyle, GUILayout.Width(84f),
                        GUILayout.Height(22f)) && pack != null)
                {
                    if (!thrall.ReturnTool(pack))
                        ThrallsPlugin.Say("No room in your inventory for it.");
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (pack == null) return;

            GUILayout.Space(6f);

            var offered = 0;
            GUILayout.BeginHorizontal();

            foreach (var item in pack.GetAllItems())
            {
                if (item == null || item.m_dropPrefab == null) continue;

                var prefab = item.m_dropPrefab.name;
                if (prefab == thrall.Tool) continue;
                if (!IsThrallTool(prefab)) continue;

                if (offered > 0 && offered % 3 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4f);
                    GUILayout.BeginHorizontal();
                }

                if (GUILayout.Button("Give " + PrettyItem(prefab), _chipStyle,
                        GUILayout.Width(170f), GUILayout.Height(22f)))
                {
                    thrall.GiveTool(item, pack);
                    GUILayout.EndHorizontal();
                    return;
                }

                offered++;
            }

            if (offered == 0)
                GUILayout.Label("You have no tools to give it.", _mutedStyle);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static bool IsThrallTool(string prefab)
        {
            foreach (var job in new[] { ThrallJob.Chop, ThrallJob.Mine, ThrallJob.Farm })
                foreach (var name in Thrall.ToolsFor(job).Split(','))
                    if (string.Equals(name.Trim(), prefab, System.StringComparison.OrdinalIgnoreCase))
                        return true;

            return false;
        }

        /// <summary>"AxeBronze" reads better as "Axe bronze" than as a prefab name.</summary>
        private static string PrettyItem(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return "";

            var text = "";
            for (int i = 0; i < prefab.Length; i++)
            {
                if (i > 0 && char.IsUpper(prefab[i])) text += " " + char.ToLowerInvariant(prefab[i]);
                else text += prefab[i];
            }
            return text;
        }

        /// <summary>One labelled line of the stat block.</summary>
        private static void Stat(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _mutedStyle, GUILayout.Width(150f));
            GUILayout.Label(value, _rowNameStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
        }

        private static GUIStyle _lockedName;

        private static GUIStyle LockedName()
        {
            if (_lockedName == null)
                _lockedName = new GUIStyle(_cardNameStyle) { normal = { textColor = Locked } };
            return _lockedName;
        }

        /// <summary>A one pixel frame, which is what separates a card from a coloured slab.</summary>
        private static void Outline(Rect r, Color color)
        {
            var tex = Solid(color);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), tex);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), tex);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), tex);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), tex);
        }

        // ------------------------------------------------------------- roster

        private static void DrawRoster(float width)
        {
            var thralls = ThrallRegistry.All;

            var total = 0;
            var working = 0;
            for (int i = 0; i < thralls.Count; i++)
            {
                if (thralls[i] == null) continue;
                total++;
                if (ThrallRegistry.IsWork(thralls[i].Job)) working++;
            }

            GUILayout.BeginHorizontal();
            FilterPill(Filter.All, "All " + total);
            FilterPill(Filter.Working, "Working " + working);
            FilterPill(Filter.Idle, "Idle " + (total - working));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            _scroll = GUILayout.BeginScrollView(_scroll, false, true,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));

            var rowWidth = width - 14f;
            var shown = 0;

            for (int i = 0; i < thralls.Count; i++)
            {
                var thrall = thralls[i];
                if (thrall == null) continue;

                var busy = ThrallRegistry.IsWork(thrall.Job);
                if (_filter == Filter.Working && !busy) continue;
                if (_filter == Filter.Idle && busy) continue;

                DrawRow(thrall, rowWidth);
                shown++;
            }

            if (shown == 0)
                GUILayout.Label("Nobody here. Raise one from the left.", _mutedStyle);

            GUILayout.EndScrollView();
        }

        private static void FilterPill(Filter which, string label)
        {
            var on = _filter == which;
            var size = _pillStyle.CalcSize(new GUIContent(label));
            var r = GUILayoutUtility.GetRect(size.x + 22f, 22f, GUILayout.Height(22f),
                GUILayout.Width(size.x + 22f));

            if (on) GUI.DrawTexture(r, _pillTex);

            var style = on ? _rowNameStyle : _mutedStyle;
            var previous = style.alignment;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, label, style);
            style.alignment = previous;

            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _filter = which;

            GUILayout.Space(6f);
        }

        /// <summary>
        /// One thrall as a card: what it is and what it is up to, and nothing to press
        /// except the card itself. Eight job buttons on every row turned the overview into
        /// a wall of controls you had to read past to find out anything.
        /// </summary>
        private static void DrawRow(Thrall thrall, float width)
        {
            var card = GUILayoutUtility.GetRect(width, 62f, GUILayout.Height(62f));

            var busy = ThrallRegistry.IsWork(thrall.Job);
            var hot = card.Contains(Event.current.mousePosition);

            GUI.DrawTexture(card, hot ? _chipTex : _cardOffTex);
            Outline(card, busy ? new Color(0.42f, 0.49f, 0.30f) : Hair);

            var pad = 10f;
            var right = new GUIStyle(_mutedStyle) { alignment = TextAnchor.MiddleRight };

            var first = new Rect(card.x + pad, card.y + 7f, card.width - pad * 2f, 20f);
            GUI.Label(first, thrall.ThrallName, _rowNameStyle);
            GUI.Label(first, thrall.TierName + " lv" + thrall.Rank, right);

            var second = new Rect(card.x + pad, card.y + 28f, card.width - pad * 2f, 18f);

            var doing = thrall.Hauling ? "hauling to the chest" : WorkNode.JobName(thrall.Job);
            GUI.Label(second, doing, busy ? _liveStyle : _mutedStyle);

            var carried = thrall.Carrying.NrOfItems();
            var slots = thrall.Carrying.GetWidth() * thrall.Carrying.GetHeight();
            var distance = Player.m_localPlayer != null
                ? Mathf.RoundToInt(Vector3.Distance(Player.m_localPlayer.transform.position,
                    thrall.transform.position))
                : 0;

            GUI.Label(second, string.Format("{0}/{1} carried · {2}m{3}", carried, slots, distance,
                thrall.HasDropOff ? "" : " · no chest"), right);

            // Experience along the bottom edge of the card.
            var track = new Rect(card.x + pad, card.yMax - 11f, card.width - pad * 2f, 3f);
            GUI.DrawTexture(track, _hairTex);

            if (thrall.Rank < Levels.MaxLevel)
            {
                var fill = Levels.Fraction(thrall.Xp);
                if (fill > 0f)
                {
                    var previous = GUI.color;
                    GUI.color = new Color(0.561f, 0.627f, 0.416f, 0.95f);
                    GUI.DrawTexture(new Rect(track.x, track.y, track.width * fill, track.height),
                        Texture2D.whiteTexture);
                    GUI.color = previous;
                }
            }

            if (GUI.Button(card, GUIContent.none, GUIStyle.none))
            {
                _subject = thrall;
                _view = View.Thrall;
            }

            GUILayout.Space(6f);
        }


        /// <summary>
        /// Experience towards the next level, as a hairline bar under the row. Nothing to
        /// click - it is earned by working - so it wants to read without being read.
        /// </summary>
        private static void DrawProgress(Thrall thrall, float width)
        {
            var track = GUILayoutUtility.GetRect(width, 3f, GUILayout.Height(3f));

            if (thrall.Rank >= Levels.MaxLevel)
            {
                GUI.DrawTexture(track, _hairTex);
                return;
            }

            GUI.DrawTexture(track, _hairTex);

            var fill = Levels.Fraction(thrall.Xp);
            if (fill <= 0f) return;

            var previous = GUI.color;
            GUI.color = new Color(0.561f, 0.627f, 0.416f, 0.95f);
            GUI.DrawTexture(new Rect(track.x, track.y, track.width * fill, track.height),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void JobChip(Thrall thrall, ThrallJob job, string label)
        {
            var active = thrall.Job == job;
            var r = GUILayoutUtility.GetRect(56f, 20f, GUILayout.Width(56f), GUILayout.Height(20f));

            GUI.DrawTexture(r, active ? _chipOnTex : _chipTex);
            if (active) Outline(r, new Color(0.49f, 0.56f, 0.35f));

            var style = active ? _liveStyle : _mutedStyle;
            var previous = style.alignment;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, label, style);
            style.alignment = previous;

            GUILayout.Space(5f);

            if (!GUI.Button(r, GUIContent.none, GUIStyle.none)) return;

            if (job == ThrallJob.Follow)
            {
                thrall.ToggleFollow(thrall.transform.position);
            }
            else if (thrall.Refusal(job).Length > 0)
            {
                ThrallsPlugin.Say(thrall.Refusal(job));
            }
            else if (ThrallRegistry.IsWork(job) && !ThrallRegistry.HasFreeSlot(thrall))
            {
                ThrallsPlugin.Say(string.Format(
                    "Only {0} thralls can work at once. Build more station upgrades near the altar.",
                    ThrallAltar.Slots));
            }
            else
            {
                thrall.ReassignHere(job);
            }
        }

        // ------------------------------------------------------------- footer

        private static void DrawFooter()
        {
            var thralls = ThrallRegistry.All;

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Recall all", _footStyle, GUILayout.Width(104f),
                    GUILayout.Height(26f)) && _altar != null)
            {
                for (int i = 0; i < thralls.Count; i++)
                    if (thralls[i] != null)
                        thralls[i].SummonTo(_altar.SummonSpot());
            }

            GUILayout.Space(8f);

            if (BuildPlans.Count > 0
                && GUILayout.Button("Cancel " + BuildPlans.Count + " build orders", _footStyle,
                    GUILayout.Width(176f), GUILayout.Height(26f)))
            {
                BuildPlans.Clear();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Close", _footStyle, GUILayout.Width(96f), GUILayout.Height(26f)))
                Close();

            GUILayout.EndHorizontal();
        }
    }
}
