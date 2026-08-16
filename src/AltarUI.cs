using System.Collections.Generic;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// The altar's ledger. Plain IMGUI so the mod stays a single DLL with no asset bundle.
    ///
    /// Full screen, in the shape Boon's panel settled on: a black field over the whole
    /// screen, a centred board with no frame around it, and three columns separated by
    /// hairlines. There is no window and nothing to drag.
    ///
    /// It was a 940x590 draggable window before, in three screens - overview, breed, thrall
    /// - and the reason to change is not that a window looked wrong. It is that a window
    /// that size cannot hold a crew and the thing you are doing to one of them at the same
    /// time, so reading the roster and giving an order were different screens with a back
    /// button between them. Full screen pays for a third column, and the third column is
    /// the two other screens standing still while you work down the list.
    ///
    /// Borrowing a frame is what Boon spent three designs learning not to do: IMGUI cannot
    /// draw with the game's shaders, so a copied sprite arrives the wrong colour and reads
    /// as an imitation of a Valheim window rather than one. A black field, the game's own
    /// fonts and flat bands have nothing that can arrive wrong.
    /// </summary>
    internal static class AltarUI
    {
        // The board is capped rather than glued to the screen: past about 1500 the roster
        // cards stretch into letterboxes and the eye has to travel the whole monitor to
        // read one row. Everything wider than the cap becomes margin.
        private const float BoardMaxW = 1500f;
        private const float BoardMaxH = 940f;
        private const float MarginX = 56f;
        private const float MarginY = 44f;

        private const float HeadH = 34f;
        private const float BarH = 4f;
        private const float HeadGap = 18f;
        private const float FootH = 20f;
        private const float FootGap = 10f;

        private const float RailW = 252f;
        private const float DetailW = 336f;
        private const float ColGap = 26f;

        private static ThrallAltar _altar;
        private static Vector2 _railScroll, _fieldScroll, _detailScroll;
        private static readonly Dictionary<Thrall, string> NameBuffers = new Dictionary<Thrall, string>();

        private enum Filter { All, Working, Idle }
        private static Filter _filter = Filter.All;

        /// <summary>
        /// What the right hand column is showing. This used to be a <c>View</c> that swapped
        /// the whole panel, which is why picking a breed hid the roster you were picking it
        /// against. Now it only ever decides one column.
        /// </summary>
        private enum Focus { None, Breed, Thrall }

        private static Focus _focus = Focus.None;
        private static int _breed = 1;
        private static Thrall _subject;

        public static bool IsOpen { get; private set; }

        public static void Toggle(ThrallAltar altar)
        {
            if (IsOpen) { Close(); return; }
            _altar = altar;
            IsOpen = true;
            _focus = Focus.None;
            _subject = null;
            _railScroll = _fieldScroll = _detailScroll = Vector2.zero;
            NameBuffers.Clear();
        }

        public static void Close()
        {
            IsOpen = false;
            _altar = null;
            _subject = null;
            _focus = Focus.None;
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

            var oldSkin = GUI.skin;
            GUI.skin = Skin();
            GUI.backgroundColor = Color.white;

            // The whole screen, so nothing behind it competes and there is no frame that
            // has to hold its own beside the game's own windows. Very nearly opaque rather
            // than opaque: the last three percent is the only thing telling you the world
            // is still there and you are standing at an altar in it.
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _voidTex);

            var boardW = Mathf.Min(Screen.width - MarginX * 2f, BoardMaxW);
            var boardH = Mathf.Min(Screen.height - MarginY * 2f, BoardMaxH);
            var left = (Screen.width - boardW) * 0.5f;
            var top = (Screen.height - boardH) * 0.5f;

            var y = top;
            DrawHead(left, ref y, boardW);

            var bodyTop = y;
            var bodyH = top + boardH - FootH - FootGap - bodyTop;

            var fieldX = left + RailW + ColGap;
            var fieldW = boardW - RailW - DetailW - ColGap * 2f;
            var detailX = left + boardW - DetailW;

            // The two hairlines that do the work a window border used to.
            Rule(new Rect(fieldX - ColGap * 0.5f, bodyTop, 1f, bodyH), _hairTex);
            Rule(new Rect(detailX - ColGap * 0.5f, bodyTop, 1f, bodyH), _hairTex);

            GUILayout.BeginArea(new Rect(left, bodyTop, RailW, bodyH));
            DrawRail(bodyH);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(fieldX, bodyTop, fieldW, bodyH));
            DrawRoster(fieldW, bodyH);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(detailX, bodyTop, DetailW, bodyH));
            DrawDetail(DetailW, bodyH);
            GUILayout.EndArea();

            DrawFoot(left, top + boardH - FootH, boardW);

            GUI.skin = oldSkin;
        }

        // ------------------------------------------------------------- palette

        private static readonly Color Void = new Color(0.035f, 0.031f, 0.027f, 0.97f);
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
        private static Texture2D _panelTex, _stripTex, _hairTex, _edgeTex, _voidTex;
        private static Texture2D _fieldTex, _chipTex, _chipOnTex, _cardOnTex, _cardOffTex, _pillTex;

        private static GUIStyle _titleStyle, _boardStyle, _metaStyle, _sectionStyle, _rowNameStyle;
        private static GUIStyle _mutedStyle, _liveStyle, _chipStyle, _pillStyle;
        private static GUIStyle _cardNameStyle, _cardMetaStyle, _cardLockedStyle;
        private static GUIStyle _nameStyle, _footStyle, _disabledStyle, _disabledChip;
        private static GUIStyle _hintStyle, _loreStyle;

        internal static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        /// <summary>
        /// The panel chrome, shared with the thrall's own orders panel.
        ///
        /// Internal rather than private so ThrallTalk can wear it. Two windows that are
        /// meant to read as the same piece of furniture cannot each build their own skin
        /// and stay in step - the first attempt at that was already a shade off on the
        /// button hover before it was ever shown to anyone. The window styles below are
        /// still built even though the ledger no longer uses a window: ThrallTalk does,
        /// and it is a small panel you open standing in front of one thrall, which is a
        /// different thing from the ledger and should stay one.
        /// </summary>
        internal static GUISkin Skin()
        {
            if (_skin != null) return _skin;

            _voidTex = Solid(Void);
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
            // No padding: bands are placed by hand, edge to edge.
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

        internal static void EnsureStyles()
        {
            if (_titleStyle != null) return;
            var skin = Skin();

            _titleStyle = new GUIStyle(skin.label)
            {
                fontSize = 20,
                normal = { textColor = Brass },
                alignment = TextAnchor.MiddleLeft
            };

            // The board's own title, bigger than the shared one because it sits on a whole
            // screen rather than in a box - at 20 it read as a caption floating in the
            // dark. Kept separate rather than raising TitleStyle: ThrallTalk draws its
            // header in a 26 high rect, and 26pt text in a 26px rect loses its descenders.
            _boardStyle = new GUIStyle(_titleStyle) { fontSize = 26 };

            _metaStyle = new GUIStyle(skin.label)
            {
                fontSize = 13,
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

            _hintStyle = new GUIStyle(skin.label)
            {
                fontSize = 12,
                normal = { textColor = Locked },
                wordWrap = true
            };

            _loreStyle = new GUIStyle(_mutedStyle) { fontSize = 13, wordWrap = true };

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

        // The styles and the two band textures, handed out so the thrall's orders panel
        // draws with the same brush rather than a near-miss of it.
        internal static GUIStyle TitleStyle { get { EnsureStyles(); return _titleStyle; } }
        internal static GUIStyle MetaStyle { get { EnsureStyles(); return _metaStyle; } }
        internal static GUIStyle SectionStyle { get { EnsureStyles(); return _sectionStyle; } }
        internal static GUIStyle MutedStyle { get { EnsureStyles(); return _mutedStyle; } }
        internal static GUIStyle LiveStyle { get { EnsureStyles(); return _liveStyle; } }
        internal static GUIStyle RowNameStyle { get { EnsureStyles(); return _rowNameStyle; } }
        internal static GUIStyle ChipStyle { get { EnsureStyles(); return _chipStyle; } }
        internal static GUIStyle FootStyle { get { EnsureStyles(); return _footStyle; } }
        internal static Texture2D StripTexture { get { Skin(); return _stripTex; } }
        internal static Texture2D HairTexture { get { Skin(); return _hairTex; } }
        internal static Color EdgeColour { get { return Edge; } }

        internal static void HairLine(float width)
        {
            var r = GUILayoutUtility.GetRect(width, 1f, GUILayout.Height(1f));
            GUI.DrawTexture(r, _hairTex);
        }

        /// <summary>
        /// A wrapping paragraph, measured rather than left to the layout.
        ///
        /// GUILayout.Label with wordWrap works out its own height from the width it is
        /// given - except inside a scroll view, where the first layout pass runs before
        /// the scrollbar has claimed its lane and the height comes out one line short.
        /// The breed lore lost its last line to exactly this and read "will put a tree
        /// through a wall if the wall is clo". CalcHeight is measured against the width
        /// we are actually going to draw at, so there is no pass to be wrong about.
        /// </summary>
        private static void Paragraph(string text, GUIStyle style, float width)
        {
            var height = style.CalcHeight(new GUIContent(text), width);
            var rect = GUILayoutUtility.GetRect(width, height,
                GUILayout.Width(width), GUILayout.Height(height));

            GUI.Label(rect, text, style);
        }

        // ------------------------------------------------------------- head and foot

        /// <summary>
        /// Title, the two counts, and a bar for the one of them that is a limit rather than
        /// a tally. Work slots fill up and stop you; the bound count is just how many you
        /// have, so only the first gets drawn as something running out.
        /// </summary>
        private static void DrawHead(float x, ref float y, float width)
        {
            GUI.Label(new Rect(x, y, width * 0.6f, HeadH),
                _altar != null ? _altar.GetHoverName() : "Bindstone", _boardStyle);

            var working = ThrallRegistry.WorkingCount();
            var slots = Mathf.Max(1, ThrallAltar.Slots);

            GUI.Label(new Rect(x + width * 0.4f, y, width * 0.6f, HeadH),
                string.Format("{0} of {1} at work   ·   {2} of {3} bound",
                    working, ThrallAltar.Slots,
                    ThrallRegistry.Count(), ThrallConfig.MaxThralls.Value),
                _metaStyle);

            y += HeadH;

            GUI.DrawTexture(new Rect(x, y, width, BarH), _hairTex);

            var fill = Mathf.Clamp01(working / (float)slots);
            if (fill > 0f)
            {
                var previous = GUI.color;
                GUI.color = Brass;
                GUI.DrawTexture(new Rect(x, y, width * fill, BarH), Texture2D.whiteTexture);
                GUI.color = previous;
            }

            y += BarH + HeadGap;
        }

        private static void DrawFoot(float x, float y, float width)
        {
            GUI.Label(new Rect(x, y, width * 0.6f, FootH),
                "Escape to close  ·  pick from the left, or a thrall from the middle", _mutedStyle);

            var thralls = ThrallRegistry.All;
            var rect = new Rect(x + width - 112f, y - 4f, 112f, 26f);

            if (GUI.Button(rect, "Recall all", _footStyle) && _altar != null)
            {
                for (int i = 0; i < thralls.Count; i++)
                    if (thralls[i] != null)
                        thralls[i].SummonTo(_altar.SummonSpot());
            }
        }

        // ------------------------------------------------------------- left rail

        private static void DrawRail(float height)
        {
            _railScroll = GUILayout.BeginScrollView(_railScroll, false, true,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(height));

            // Leave the scrollbar its lane rather than letting cards run under it.
            var w = RailW - 14f;

            GUILayout.Label("Raise", _sectionStyle);
            GUILayout.Space(4f);

            var player = Player.m_localPlayer;

            for (int tier = 1; tier <= ThrallBreed.Count; tier++)
            {
                var unlocked = ThrallBreed.Unlocked(tier);

                var cost = ThrallBreed.RaiseCost(tier);
                var paid = player != null && (string.IsNullOrEmpty(cost)
                                              || ItemCost.CanPay(player.GetInventory(), cost));

                var detail = !unlocked
                    ? ThrallBreed.Blocker(tier)
                    : paid ? "materials ready" : "not enough materials";

                TierCard(tier, ThrallBreed.NameFor(tier), detail, unlocked, w);
                GUILayout.Space(6f);
            }

            // No "The altar" section here. It was a card you could not press, describing a
            // piece you build with the hammer - so it sat in a column of things you pick
            // and was the one thing in it that did nothing. What it had to say is said
            // where it is needed instead: a breed the altar cannot yet call reads "needs
            // bindstone upgrade 1" on its own card, and again in the detail column.

            // The ones you sent away, waiting to be called back as themselves.
            if (Resting.Count > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Resting", _sectionStyle);
                GUILayout.Space(4f);

                var resting = new List<RestingThrall>(Resting.All);
                foreach (var entry in resting)
                {
                    // Same card as the fallen below, because waking one now costs goods
                    // too: it says the price and lights up when you can pay, rather than
                    // offering a button that refuses.
                    var cost = ThrallBreed.RecallCost(entry.Tier);
                    var free = string.IsNullOrEmpty(cost);
                    var affordable = free
                        || (player != null && ItemCost.CanPay(player.GetInventory(), cost));

                    var card = GUILayoutUtility.GetRect(w, 54f, GUILayout.Width(w), GUILayout.Height(54f));
                    GUI.DrawTexture(card, affordable ? _cardOnTex : _cardOffTex);
                    Outline(card, affordable ? new Color(0.49f, 0.56f, 0.35f) : Hair);

                    GUI.Label(new Rect(card.x, card.y, card.width, 20f),
                        entry.Name + "  ·  " + entry.TierName + " lv" + entry.Level,
                        _cardNameStyle);
                    GUI.Label(new Rect(card.x, card.y + 18f, card.width, 18f),
                        free ? "free" : ItemCost.Describe(cost),
                        affordable ? _cardMetaStyle : _cardLockedStyle);

                    if (GUI.Button(new Rect(card.x + 9f, card.y + 34f, 84f, 16f),
                            "Call back", _chipStyle) && _altar != null)
                    {
                        ThrallsPlugin.Recall(entry, _altar.SummonSpot());
                    }

                    GUILayout.Space(6f);
                }
            }

            if (Fallen.Count > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Fallen", _sectionStyle);
                GUILayout.Space(4f);

                var roll = new List<FallenThrall>(Fallen.All);
                foreach (var entry in roll)
                {
                    var cost = ThrallBreed.ResurrectCost(entry.Tier);
                    var affordable = player != null && ItemCost.CanPay(player.GetInventory(), cost);

                    var card = GUILayoutUtility.GetRect(w, 54f, GUILayout.Width(w), GUILayout.Height(54f));
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

            GUILayout.FlexibleSpace();
            GUILayout.EndScrollView();
        }

        private static void TierCard(int tier, string name, string detail, bool unlocked, float w)
        {
            var card = GUILayoutUtility.GetRect(w, 46f, GUILayout.Width(w), GUILayout.Height(46f));

            var chosen = _focus == Focus.Breed && _breed == tier;

            GUI.DrawTexture(card, unlocked ? _cardOnTex : _cardOffTex);
            Outline(card, chosen ? Brass
                                 : unlocked ? new Color(0.49f, 0.56f, 0.35f)
                                            : new Color(0.353f, 0.298f, 0.220f));

            GUI.Label(new Rect(card.x, card.y, card.width, 22f), name,
                unlocked ? _cardNameStyle : LockedName());

            GUI.Label(new Rect(card.x, card.y + 21f, card.width, 20f), detail,
                unlocked ? _cardMetaStyle : _cardLockedStyle);

            // The card fills the right hand column rather than binding on the spot. A
            // single click that spends silver and a golem's head with no chance to read
            // what you are getting is a click people regret.
            if (GUI.Button(card, GUIContent.none, GUIStyle.none))
            {
                _breed = tier;
                _focus = Focus.Breed;
                _detailScroll = Vector2.zero;
            }
        }

        // ------------------------------------------------------------- roster field

        private static void DrawRoster(float width, float height)
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

            GUILayout.Space(10f);

            _fieldScroll = GUILayout.BeginScrollView(_fieldScroll, false, true,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));

            var shown = new List<Thrall>();
            for (int i = 0; i < thralls.Count; i++)
            {
                var thrall = thralls[i];
                if (thrall == null) continue;

                var busy = ThrallRegistry.IsWork(thrall.Job);
                if (_filter == Filter.Working && !busy) continue;
                if (_filter == Filter.Idle && busy) continue;

                shown.Add(thrall);
            }

            if (shown.Count == 0)
            {
                GUILayout.Label("Nobody here. Raise one from the left.", _mutedStyle);
                GUILayout.EndScrollView();
                return;
            }

            // Two columns once there is room for two readable cards, one otherwise. A card
            // narrower than about 330 cannot hold a name and "hauling to the depot · 42m"
            // on the same two lines without one of them clipping, and a clipped distance
            // still looks like a distance.
            const float gap = 12f;
            var inner = width - 14f;
            var columns = inner >= 700f ? 2 : 1;
            var cardW = (inner - gap * (columns - 1)) / columns;

            for (int i = 0; i < shown.Count; i += columns)
            {
                GUILayout.BeginHorizontal();

                for (int c = 0; c < columns; c++)
                {
                    if (i + c >= shown.Count) break;
                    DrawCard(shown[i + c], cardW);
                    if (c + 1 < columns) GUILayout.Space(gap);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(gap);
            }

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
        ///
        /// Selection is a click, not a hover. Boon's field selects on hover because a click
        /// there only ever spends a pick, so the two gestures never overlap - here the
        /// right hand column has buttons in it, and a detail that changed as the cursor
        /// crossed the field on its way to them would be unusable.
        /// </summary>
        private static void DrawCard(Thrall thrall, float width)
        {
            var card = GUILayoutUtility.GetRect(width, 62f, GUILayout.Width(width), GUILayout.Height(62f));

            var busy = ThrallRegistry.IsWork(thrall.Job);
            var hot = card.Contains(Event.current.mousePosition);
            var chosen = _focus == Focus.Thrall && _subject == thrall;

            GUI.DrawTexture(card, hot ? _chipTex : _cardOffTex);
            Outline(card, chosen ? Brass : busy ? new Color(0.42f, 0.49f, 0.30f) : Hair);

            var pad = 10f;
            var right = new GUIStyle(_mutedStyle) { alignment = TextAnchor.MiddleRight };

            var first = new Rect(card.x + pad, card.y + 7f, card.width - pad * 2f, 20f);
            GUI.Label(first, thrall.ThrallName, _rowNameStyle);
            GUI.Label(first, thrall.TierName + " lv" + thrall.Rank, right);

            var second = new Rect(card.x + pad, card.y + 28f, card.width - pad * 2f, 18f);

            var doing = thrall.Hauling ? "hauling to the depot" : WorkNode.JobName(thrall.Job);
            GUI.Label(second, doing, busy ? _liveStyle : _mutedStyle);

            var carried = thrall.Carrying.NrOfItems();
            var slots = thrall.Carrying.GetWidth() * thrall.Carrying.GetHeight();
            var distance = Player.m_localPlayer != null
                ? Mathf.RoundToInt(Vector3.Distance(Player.m_localPlayer.transform.position,
                    thrall.transform.position))
                : 0;

            GUI.Label(second, string.Format("{0}/{1} carried · {2}m{3}", carried, slots, distance,
                thrall.HasDropOff ? "" : " · no depot"), right);

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
                _focus = Focus.Thrall;
                _detailScroll = Vector2.zero;
            }
        }

        // ------------------------------------------------------------- right column

        private static void DrawDetail(float width, float height)
        {
            if (_focus == Focus.Thrall && _subject == null) _focus = Focus.None;

            if (_focus == Focus.None)
            {
                GUILayout.Space(4f);
                GUILayout.Label("Nothing picked", _sectionStyle);
                GUILayout.Space(6f);
                Paragraph("Pick a breed on the left to read what it is worth raising, "
                          + "or a thrall in the middle to hand it a tool and put it "
                          + "to work.", _hintStyle, width - 14f);
                return;
            }

            _detailScroll = GUILayout.BeginScrollView(_detailScroll, false, true,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(height));

            var w = width - 14f;

            if (_focus == Focus.Breed) DrawBreedDetail(w);
            else DrawThrallDetail(w);

            GUILayout.FlexibleSpace();
            GUILayout.EndScrollView();
        }

        private static void DrawBreedDetail(float width)
        {
            var tier = ThrallBreed.Clamp(_breed);
            var unlocked = ThrallBreed.Unlocked(tier);

            GUILayout.Label(ThrallBreed.NameFor(tier), _titleStyle);
            GUILayout.Space(2f);
            Paragraph(ThrallBreed.Lore(tier), _loreStyle, width);

            GUILayout.Space(12f);
            HairLine(width);
            GUILayout.Space(10f);

            var fresh = WorkPower.For(tier, 1);
            var veteran = WorkPower.For(tier, Levels.MaxLevel);

            var green = ThrallBreed.PackSlots(tier, 1);
            Stat("Pack", (green == 1 ? "1 slot, " : green + " slots, ")
                         + ThrallBreed.PackSlots(tier, Levels.MaxLevel) + " at level " + Levels.MaxLevel);
            Stat("Chopping", fresh.Chop.ToString("0.#") + " → " + veteran.Chop.ToString("0.#") + " per swing");
            Stat("Mining", fresh.Pickaxe.ToString("0.#") + " → " + veteran.Pickaxe.ToString("0.#") + " per swing");
            Stat("Tool grade", fresh.ToolTier.ToString());
            Stat("Reach", "+" + ThrallBreed.ReachBonus(tier).ToString("0.#") + "m");
            Stat("Raising the dead", ItemCost.Describe(ThrallBreed.ResurrectCost(tier)));

            GUILayout.Space(14f);

            if (!unlocked)
            {
                Paragraph(ThrallBreed.Blocker(tier) + " before this one will answer.",
                    _hintStyle, width);
                return;
            }

            var player = Player.m_localPlayer;
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

            var previous = GUI.backgroundColor;
            GUI.backgroundColor = paid
                ? new Color(0.45f, 0.62f, 0.35f)
                : new Color(0.22f, 0.20f, 0.17f);

            var rect = GUILayoutUtility.GetRect(width, 30f,
                GUILayout.Width(width), GUILayout.Height(30f));

            // Drawn but not wired when you cannot pay: a button that looks pressable and
            // then refuses is worse than one that plainly is not.
            if (GUI.Button(rect, "Raise one", paid ? _footStyle : _disabledStyle)
                && paid && _altar != null)
            {
                ThrallsPlugin.Hire(tier, _altar.SummonSpot());
                _focus = Focus.None;
            }

            GUI.backgroundColor = previous;
        }

        private static void DrawThrallDetail(float width)
        {
            var thrall = _subject;

            GUILayout.BeginHorizontal();
            string buffer;
            if (!NameBuffers.TryGetValue(thrall, out buffer)) buffer = thrall.ThrallName;

            var edited = GUILayout.TextField(buffer, 20, _nameStyle,
                GUILayout.Width(width - 82f), GUILayout.Height(28f));
            NameBuffers[thrall] = edited;

            if (edited.Trim() != thrall.ThrallName && edited.Trim().Length > 0
                && GUILayout.Button("Rename", _chipStyle, GUILayout.Width(74f), GUILayout.Height(28f)))
            {
                thrall.Rename(edited);
                NameBuffers[thrall] = thrall.ThrallName;
            }

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

            Stat("Doing", thrall.Hauling ? "hauling to the depot" : WorkNode.JobName(thrall.Job));
            Stat("Pack", carried + " of " + slots + " slots");
            Stat("Experience", thrall.XpProgress);
            Stat("Chopping", power.Chop.ToString("0.#") + " per swing");
            Stat("Mining", power.Pickaxe.ToString("0.#") + " per swing");
            Stat("Depot", thrall.HasDropOff ? "in range of where it works" : "none within reach");
            Stat("Distance", distance + "m away");

            GUILayout.Space(14f);
            DrawToolBench(thrall, width);

            GUILayout.Space(14f);
            GUILayout.Label("Put it to work", _sectionStyle);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            JobChip(thrall, ThrallJob.None, "Idle");
            JobChip(thrall, ThrallJob.Chop, "Chop");
            JobChip(thrall, ThrallJob.Mine, "Mine");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);

            GUILayout.BeginHorizontal();
            JobChip(thrall, ThrallJob.Gather, "Gather");
            JobChip(thrall, ThrallJob.Farm, "Farm");
            JobChip(thrall, ThrallJob.Repair, "Repair");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);

            GUILayout.BeginHorizontal();
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
                _subject = null;
                _focus = Focus.None;
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
        private static void DrawToolBench(Thrall thrall, float width)
        {
            GUILayout.Label("Its tool", _sectionStyle);
            GUILayout.Space(4f);

            var player = Player.m_localPlayer;
            var pack = player != null ? player.GetInventory() : null;

            GUILayout.BeginHorizontal();

            if (thrall.Tool.Length == 0)
            {
                GUILayout.Label("empty handed", _mutedStyle, GUILayout.Width(width - 96f));
            }
            else
            {
                GUILayout.Label(PrettyItem(thrall.Tool), _liveStyle, GUILayout.Width(width - 96f));

                if (GUILayout.Button("Take back", _chipStyle, GUILayout.Width(88f),
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

            // One per line in a 336 wide column. Three across was right in a 940 window and
            // is not here: at 100px a chip the labels came out "Give Axe bro".
            var offered = 0;

            foreach (var item in pack.GetAllItems())
            {
                if (item == null || item.m_dropPrefab == null) continue;

                var prefab = item.m_dropPrefab.name;
                if (prefab == thrall.Tool) continue;
                if (!IsThrallTool(prefab)) continue;

                if (GUILayout.Button("Give " + PrettyItem(prefab), _chipStyle,
                        GUILayout.Width(width), GUILayout.Height(22f)))
                {
                    thrall.GiveTool(item, pack);
                    return;
                }

                GUILayout.Space(4f);
                offered++;
            }

            if (offered == 0)
                GUILayout.Label("You have no tools to give it.", _mutedStyle);
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
        internal static string PrettyItem(string prefab)
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

        /// <summary>
        /// One labelled line of the stat block. The label column is 118 rather than the 150
        /// it was in the window: the detail column is 336 wide and at 150 every value had
        /// less room than its own label.
        /// </summary>
        internal static void Stat(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _mutedStyle, GUILayout.Width(118f));
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

        private static void JobChip(Thrall thrall, ThrallJob job, string label)
        {
            var active = thrall.Job == job;
            var r = GUILayoutUtility.GetRect(64f, 22f, GUILayout.Width(64f), GUILayout.Height(22f));

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
                    "Only {0} thralls can work at once. Build more station upgrades near the bindstone.",
                    ThrallAltar.Slots));
            }
            else
            {
                thrall.ReassignHere(job);

                if (job == ThrallJob.Chop && thrall.Smashes)
                    ThrallsPlugin.Say(thrall.ThrallName + " will knock trees down rather than "
                                      + "cut them, so expect little wood.");
            }
        }
    }
}
