using BepInEx.Configuration;
using UnityEngine;

namespace Thralls
{
    /// <summary>
    /// Hotkey reads go through the game's own input layer rather than UnityEngine.Input.
    /// ZInput sits on the new Input System, which reads keys by scancode - so numpad
    /// bindings work whether or not Num Lock happens to be on.
    /// </summary>
    internal static class Hotkey
    {
        public static bool Down(ConfigEntry<KeyboardShortcut> entry)
        {
            if (entry == null) return false;

            var shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None) return false;

            foreach (var modifier in shortcut.Modifiers)
                if (!ZInput.GetKey(modifier, false)) return false;

            return ZInput.GetKeyDown(shortcut.MainKey, false);
        }
    }
}
