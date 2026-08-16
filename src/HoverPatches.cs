using HarmonyLib;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Hover text for thralls, plus the two patches that make an IMGUI
    /// panel usable: free the cursor and stop the player acting while it is open.
    /// </summary>
    [HarmonyPatch]
    internal static class HoverPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.GetHoverName))]
        private static void HoverName(Character __instance, ref string __result)
        {
            var thrall = __instance.GetComponent<Thrall>();
            if (thrall == null) return;

            var owner = thrall.OwnerName;
            __result = string.IsNullOrEmpty(owner)
                ? thrall.ThrallName
                : thrall.ThrallName + " summoned by " + owner;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.GetHoverText))]
        private static void HoverText(Character __instance, ref string __result)
        {
            var thrall = __instance.GetComponent<Thrall>();
            if (thrall == null) return;

            var carried = thrall.Carrying.NrOfItems();
            var slots = thrall.Carrying.GetWidth() * thrall.Carrying.GetHeight();
            var doing = thrall.Hauling ? "hauling to the depot" : WorkNode.JobName(thrall.Job);

            var owner = thrall.OwnerName;

            __result = thrall.ThrallName
                       + (string.IsNullOrEmpty(owner)
                           ? ""
                           : " <color=#b0a080>summoned by " + owner + "</color>")
                       + "  <color=orange>" + thrall.TierName
                       + " lv" + thrall.Rank + "</color>"
                       + "\n<color=yellow>" + doing + "</color>"
                       + "\npack " + carried + "/" + slots + "   xp " + thrall.XpProgress
                       // The prompt is the only thing that says the orders panel exists.
                       // A creature you can talk to looks exactly like one you cannot
                       // until you have tried it.
                       + (ThrallConfig.TalkOnUse.Value
                           ? "\n[<color=yellow><b>$KEY_Use</b></color>] talk to it"
                           : "");
        }

        /// <summary>
        /// The altars must exist in ZNetScene before it starts turning saved ZDOs back into
        /// objects. A prefab it cannot resolve is treated as junk and the ZDO is thrown
        /// away - which is why altars vanished on reload and had to be rebuilt.
        /// Both Awakes are hooked because either may run second.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static void RegisterOnScene()
        {
            AltarPrefab.Register();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static void RegisterOnObjectDb()
        {
            AltarPrefab.Register();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
        private static void RegisterOnObjectDbCopy()
        {
            AltarPrefab.Register();
        }

        /// <summary>
        /// Escape closes the bindstone panel instead of opening the game menu.
        ///
        /// Menu.Update calls Show() on escape, so this catches the call rather than the
        /// key: it means the panel closes on the same press that would otherwise have
        /// dropped you into the main menu, which is what every other window in the game
        /// does.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
        private static bool EscapeClosesAltar()
        {
            if (AltarUI.IsOpen) { AltarUI.Close(); return false; }
            if (ThrallTalk.IsOpen) { ThrallTalk.Close(); return false; }
            return true;
        }

        /// <summary>
        /// The map builds its own controls in Start, so the copy has to be taken after
        /// that has run or there is nothing to copy.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Minimap), "Start")]
        private static void AddMapToggle(Minimap __instance)
        {
            ThrallMapToggle.Build(__instance);
        }

        /// <summary>
        /// Either of the mod's windows. Both need the same four things doing - input
        /// blocked, the look pinned, the controller ignored and the cursor freed - and a
        /// window that gets three of the four is a window you can click while the
        /// character swings an axe at whatever is in front of it.
        /// </summary>
        private static bool PanelOpen
        {
            get { return AltarUI.IsOpen || ThrallTalk.IsOpen; }
        }

        /// <summary>
        /// Pressing use on a thrall opens its orders panel.
        ///
        /// Patched on Player.Interact rather than on an Interactable of our own, because
        /// what a creature does with a use press depends on which components it happens to
        /// carry: the game takes the first Interactable it finds with GetComponentInParent,
        /// so a thrall built on a creature that has a Tameable would hand the press to that
        /// instead, and one built on a creature without would have no Interactable at all
        /// and swallow it. Catching it here is the same answer for every breed.
        ///
        /// Only a tap. A hold is left alone - that is the gesture the game reserves for a
        /// piece's secondary action, and taking it would break anything a modded creature
        /// does with it.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "Interact",
            new[] { typeof(GameObject), typeof(bool), typeof(bool) })]
        private static bool TalkOnInteract(GameObject go, bool hold)
        {
            if (!ThrallConfig.TalkOnUse.Value || hold || go == null) return true;

            var thrall = go.GetComponentInParent<Thrall>();
            if (thrall == null) return true;

            ThrallTalk.Toggle(thrall);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "TakeInput")]
        private static void BlockInput(ref bool __result)
        {
            if (PanelOpen) __result = false;
        }

        /// <summary>
        /// Stops the character turning while the bindstone panel is open.
        ///
        /// Player.TakeInput above blocks actions but not the camera: mouse look lives in
        /// PlayerController.LateUpdate, behind its own private TakeInput and this test.
        /// InInventoryEtc is what an open inventory or store trips to pin the look, so
        /// tripping it here is exactly how the workbench behaves rather than an
        /// impression of it.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerController), "InInventoryEtc")]
        private static void HoldLookStill(ref bool __result)
        {
            if (PanelOpen) __result = true;
        }

        /// <summary>
        /// The same panel should also swallow the movement and action input that
        /// PlayerController reads, not just the look.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        private static void BlockControllerInput(ref bool __result)
        {
            if (PanelOpen) __result = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
        private static void FreeCursor()
        {
            if (!PanelOpen) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}

