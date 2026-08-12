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
            var doing = thrall.Hauling ? "hauling to the chest" : WorkNode.JobName(thrall.Job);

            var owner = thrall.OwnerName;

            __result = thrall.ThrallName
                       + (string.IsNullOrEmpty(owner)
                           ? ""
                           : " <color=#b0a080>summoned by " + owner + "</color>")
                       + "  <color=orange>" + thrall.TierName
                       + " lv" + thrall.Rank + "</color>"
                       + "\n<color=yellow>" + doing + "</color>"
                       + "\npack " + carried + "/" + slots + "   xp " + thrall.XpProgress;
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
        /// Escape closes the altar panel instead of opening the game menu.
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
            if (!AltarUI.IsOpen) return true;

            AltarUI.Close();
            return false;
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "TakeInput")]
        private static void BlockInput(ref bool __result)
        {
            if (AltarUI.IsOpen) __result = false;
        }

        /// <summary>
        /// Stops the character turning while the altar panel is open.
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
            if (AltarUI.IsOpen) __result = true;
        }

        /// <summary>
        /// The same panel should also swallow the movement and action input that
        /// PlayerController reads, not just the look.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        private static void BlockControllerInput(ref bool __result)
        {
            if (AltarUI.IsOpen) __result = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
        private static void FreeCursor()
        {
            if (!AltarUI.IsOpen) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}

