using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// What is left of the site tools: one testing crutch that is not key-driven.
    ///
    /// This file used to hold three things - force the time of day, toggle god mode, and
    /// flatten a wide patch of ground - and all three were reachable only from a numpad
    /// key. Thralls binds no keys now, so all three went with the bindings.
    ///
    /// Time and god mode were deleted rather than moved: Devkit already has both, and its
    /// clock reads off EnvMan.CalculateDay rather than guessing at phase times the way the
    /// version here did. Flatten moved to Devkit intact, because it is a build cheat and
    /// Devkit is the mod that never reaches a player.
    ///
    /// KeepAlive stays because nothing about it was a key: it is driven by the GodMode
    /// setting, read every frame, and is how a build-and-test session avoids being
    /// interrupted by dying.
    /// </summary>
    internal static class SiteTools
    {
        /// <summary>
        /// Development crutch: keeps health and stamina topped up and turns on the game's
        /// own god mode, so building and testing is not interrupted by dying or resting.
        ///
        /// Off unless the GodMode setting is switched on by hand in the cfg. There is no
        /// longer a key for it, and deliberately no menu entry either - Devkit's God
        /// button is where a button for this belongs.
        /// </summary>
        public static void KeepAlive()
        {
            if (!ThrallConfig.GodMode.Value) return;

            var player = Player.m_localPlayer;
            if (player == null || player.IsDead()) return;

            if (!player.InGodMode()) player.SetGodMode(true);

            player.SetHealth(player.GetMaxHealth());
            player.AddStamina(player.GetMaxStamina());
            player.AddEitr(100f);
        }
    }
}
