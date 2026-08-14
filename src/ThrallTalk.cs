using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// The panel that opens when you walk up to a thrall and press use.
    ///
    /// The altar's ledger is for running a crew - every thrall in a list, hiring, the roll
    /// of the dead. This is for talking to one of them where it stands, and the difference
    /// matters for one order in particular: "work from here" means the ground under your
    /// feet, and there is no way to say that from a panel you opened at the altar.
    ///
    /// Laid out as the field ledger of the three mockups: what it is doing down the left,
    /// what you can tell it down the right. The left rail is not decoration - the two
    /// distances on it, to the depot and to its base, are the numbers that tell you whether
    /// this thrall is set up sensibly, and they are precisely what you have walked over to
    /// find out.
    /// </summary>
    internal static class ThrallTalk
    {
        private const int WindowId = 0x7B1A12;

        private const float HeaderH = 40f;
        private const float FooterH = 34f;
        // Wide enough for the longest value the rail can hold - "42m from here" beside
        // its label - because a rail that clips is worse than no rail: a truncated
        // distance still looks like a distance.
        private const float RailW = 196f;
        private const float Pad = 12f;

        private static Rect _window = new Rect(0f, 0f, 470f, 268f);
        private static bool _placed;
        private static Thrall _subject;

        /// <summary>Shown instead of the order list while a job is being chosen.</summary>
        private static bool _choosingJob;

        public static bool IsOpen { get; private set; }

        public static Thrall Subject { get { return _subject; } }

        public static void Open(Thrall thrall)
        {
            if (thrall == null) return;

            _subject = thrall;
            _choosingJob = false;
            IsOpen = true;
        }

        public static void Toggle(Thrall thrall)
        {
            if (IsOpen && _subject == thrall) { Close(); return; }
            Open(thrall);
        }

        public static void Close()
        {
            IsOpen = false;
            _subject = null;
            _choosingJob = false;
        }

        /// <summary>
        /// Shuts itself when the thrall dies, is dismissed, or you walk off - the same
        /// three ways the altar panel closes. A panel left open on a thrall that no longer
        /// exists would keep the cursor free and the player frozen with nothing to click.
        /// </summary>
        public static void Tick()
        {
            if (!IsOpen) return;

            var player = Player.m_localPlayer;
            if (_subject == null || player == null)
            {
                Close();
                return;
            }

            if (Vector3.Distance(player.transform.position, _subject.transform.position)
                > Mathf.Max(2f, ThrallConfig.TalkWalkAway.Value))
                Close();
        }

        public static void Draw()
        {
            if (!IsOpen || _subject == null) return;

            AltarUI.EnsureStyles();

            if (!_placed)
            {
                _window.x = (Screen.width - _window.width) * 0.5f;
                _window.y = (Screen.height - _window.height) * 0.62f;
                _placed = true;
            }

            var oldSkin = GUI.skin;
            GUI.skin = AltarUI.Skin();
            GUI.backgroundColor = Color.white;

            _window = GUI.Window(WindowId, _window, DrawWindow, "");

            GUI.skin = oldSkin;
        }

        private static void DrawWindow(int id)
        {
            var thrall = _subject;
            if (thrall == null) { Close(); return; }

            var w = _window.width;
            var h = _window.height;

            GUI.DrawTexture(new Rect(0f, 0f, w, HeaderH), AltarUI.StripTexture);
            GUI.DrawTexture(new Rect(0f, HeaderH - 1f, w, 1f), AltarUI.HairTexture);
            GUI.DrawTexture(new Rect(0f, h - FooterH, w, FooterH), AltarUI.StripTexture);
            GUI.DrawTexture(new Rect(0f, h - FooterH, w, 1f), AltarUI.HairTexture);

            GUI.Label(new Rect(Pad, 6f, w - Pad * 2f - 150f, 26f),
                thrall.ThrallName, AltarUI.TitleStyle);
            GUI.Label(new Rect(w - 150f - Pad, 10f, 150f, 20f),
                thrall.TierName + ", level " + thrall.Rank, AltarUI.MetaStyle);

            var body = new Rect(0f, HeaderH, w, h - HeaderH - FooterH);
            DrawRail(thrall, new Rect(0f, body.y, RailW, body.height));
            GUI.DrawTexture(new Rect(RailW, body.y, 1f, body.height), AltarUI.HairTexture);

            var orders = new Rect(RailW + 1f, body.y, w - RailW - 1f, body.height);
            if (_choosingJob) DrawJobs(thrall, orders);
            else DrawOrders(thrall, orders);

            DrawFooter(new Rect(0f, h - FooterH, w, FooterH));

            // Dragged by the header only. Dragging from anywhere would mean a mis-clicked
            // order slides the window instead of doing nothing, and these orders are not
            // all reversible.
            GUI.DragWindow(new Rect(0f, 0f, w, HeaderH));
        }

        /// <summary>What it is doing, and the two distances that say whether that is sane.</summary>
        private static void DrawRail(Thrall thrall, Rect rail)
        {
            GUILayout.BeginArea(new Rect(rail.x + Pad, rail.y + 10f, rail.width - Pad * 2f,
                                         rail.height - 14f));

            Line("Doing", thrall.Hauling ? "hauling it in" : WorkNode.JobName(thrall.Job),
                 thrall.Hauling);

            var slots = thrall.Carrying.GetWidth() * thrall.Carrying.GetHeight();
            Line("Pack", thrall.Carrying.NrOfItems() + " / " + slots, false);

            var depot = thrall.DepotFor();
            Line("Depot",
                 depot == null
                     ? "none in range"
                     : Mathf.RoundToInt(Vector3.Distance(thrall.transform.position,
                                                         depot.transform.position)) + "m",
                 depot != null);

            Line("Base", Mathf.RoundToInt(Vector3.Distance(thrall.transform.position,
                                                           thrall.Base)) + "m from here", false);

            Line("Tool", thrall.Tool.Length == 0 ? "empty handed" : AltarUI.PrettyItem(thrall.Tool),
                 false);

            Line("Experience", thrall.XpProgress, false);

            GUILayout.EndArea();
        }

        /// <summary>
        /// One rail line. Fixed label width, so no value can push a label out of true -
        /// a rail that reflows as a pack fills is a rail that is hard to read at a glance.
        /// </summary>
        private static void Line(string label, string value, bool live)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, AltarUI.MutedStyle, GUILayout.Width(72f));
            GUILayout.Label(value, live ? AltarUI.LiveStyle : AltarUI.MutedStyle,
                            GUILayout.Width(96f));
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        private static void DrawOrders(Thrall thrall, Rect area)
        {
            // Three of the six orders are "where I am standing", and OnGUI runs several
            // times a frame - including on the frame a death or a teleport takes the
            // player away. Read it once, and if there is nobody there the panel has no
            // business being open at all.
            var player = Player.m_localPlayer;
            if (player == null) { Close(); return; }

            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 9f, area.width - 20f,
                                         area.height - 12f));

            var following = thrall.Job == ThrallJob.Follow;

            if (Order(following ? "Stop following" : "Follow me"))
            {
                thrall.ToggleFollow(player.transform.position);
                ThrallsPlugin.Say(thrall.Job == ThrallJob.Follow
                    ? thrall.ThrallName + " follows you."
                    : thrall.ThrallName + " stays put.");
                Close();
            }

            // The order this panel exists for. The base moves to where *you* are standing,
            // not to where the thrall is and not to where you are looking: you walked to
            // the spot to say it, and the spot under your feet is the one thing you can be
            // certain you meant.
            if (Order("Work from here"))
            {
                var spot = player.transform.position;
                thrall.MoveBase(spot);
                ThrallsPlugin.Say(string.Format("{0} will work within {1}m of here.",
                    thrall.ThrallName, Mathf.RoundToInt(ThrallConfig.WorkRadius.Value)));
                Close();
            }

            if (Order("Do something else"))
            {
                _choosingJob = true;
            }

            if (Order("Take your load in"))
            {
                if (thrall.SendToDepot())
                    ThrallsPlugin.Say(thrall.ThrallName + " heads for the depot.");
                else
                    ThrallsPlugin.Say("No depot within "
                                      + Mathf.RoundToInt(ThrallConfig.DepotRange.Value)
                                      + "m of where " + thrall.ThrallName + " works.");
                Close();
            }

            if (Order("Wait here"))
            {
                thrall.AssignJob(ThrallJob.None, player.transform.position);
                ThrallsPlugin.Say(thrall.ThrallName + " will wait here.");
                Close();
            }

            if (Order("Go free"))
            {
                var name = thrall.ThrallName;
                // Kept, not thrown out: it goes onto the altar's resting roll with its
                // name, level and tool, exactly as the ledger's own release does.
                thrall.Dismiss();
                ThrallsPlugin.Say(name + " is released from service.");
                Close();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// The job list, on the same panel rather than a second window.
        ///
        /// Each job is put to work at the thrall's current base, not at your feet - you
        /// are changing what it does, not where. "Work from here" is the order that moves
        /// it, and keeping the two apart is what stops a crew drifting towards the player
        /// every time somebody is retasked.
        /// </summary>
        private static void DrawJobs(Thrall thrall, Rect area)
        {
            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 9f, area.width - 20f,
                                         area.height - 12f));

            GUILayout.Label("Set it to", AltarUI.SectionStyle);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            JobChip(thrall, ThrallJob.Chop, "Chop");
            JobChip(thrall, ThrallJob.Mine, "Mine");
            JobChip(thrall, ThrallJob.Gather, "Gather");
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);

            GUILayout.BeginHorizontal();
            JobChip(thrall, ThrallJob.Farm, "Farm");
            JobChip(thrall, ThrallJob.Repair, "Repair");
            GUILayout.EndHorizontal();
            GUILayout.Space(9f);

            if (GUILayout.Button("< Back", AltarUI.ChipStyle,
                                 GUILayout.Width(70f), GUILayout.Height(22f)))
                _choosingJob = false;

            GUILayout.EndArea();
        }

        private static void JobChip(Thrall thrall, ThrallJob job, string label)
        {
            if (!GUILayout.Button(label, AltarUI.ChipStyle,
                                  GUILayout.Width(86f), GUILayout.Height(24f)))
                return;

            // The game's own refusal, word for word, rather than a second opinion about
            // what a thrall can hold.
            var refusal = thrall.Refusal(job);
            if (refusal.Length > 0)
            {
                ThrallsPlugin.Say(refusal);
                return;
            }

            thrall.AssignJob(job, thrall.Base);
            ThrallsPlugin.Say(thrall.ThrallName + " starts " + WorkNode.JobName(job) + ".");
            Close();
        }

        /// <summary>A full-width order button. Returns true on the frame it is pressed.</summary>
        private static bool Order(string label)
        {
            var pressed = GUILayout.Button(label, AltarUI.FootStyle,
                                           GUILayout.Height(24f));
            GUILayout.Space(4f);
            return pressed;
        }

        private static void DrawFooter(Rect footer)
        {
            GUI.Label(new Rect(Pad, footer.y + 9f, 240f, 18f),
                      "Nothing, carry on  -  Esc", AltarUI.MutedStyle);

            if (GUI.Button(new Rect(footer.xMax - 84f - Pad, footer.y + 5f, 84f, 24f),
                           "Close", AltarUI.FootStyle))
                Close();
        }
    }
}
