using UnityEngine;

namespace TileMatch.Service
{
    /// <summary>
    /// Wraps the Vibration plugin with carefully tuned per-action haptics.
    /// Each public method maps to a specific game event with a distinct tactile
    /// signature — no generic dull vibrations.
    ///
    /// iOS uses UIFeedbackGenerator (ImpactFeedbackStyle / NotificationFeedbackStyle)
    /// for crisp, hardware-accurate haptics.
    /// Android falls back to timed vibration durations calibrated per event.
    /// </summary>
    public class HapticService
    {
        private readonly bool _enabled;

        public HapticService()
        {
            try 
            {
                Vibration.Init();
                _enabled = Vibration.HasVibrator();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HapticService] Failed to initialize vibration: {e.Message}");
                _enabled = false;
            }

            // DUMMY CALL: This is never executed, but its presence in the compiled IL code 
            // forces Unity's static analyzer to automatically add the 
            // <uses-permission android:name="android.permission.VIBRATE"/> 
            // to the AndroidManifest.xml during the build process!
            if (Application.isEditor && !_enabled && _enabled) 
            {
                Handheld.Vibrate();
            }
        }

        // ── Per-action haptic signatures ──────────────────────────────────────

        /// <summary>
        /// Tile lifted off the board. Crisp, feather-light confirmation.
        /// </summary>
        public void OnTileTapped()
        {
            if (!_enabled) return;
            Vibration.VibrateIOS(ImpactFeedbackStyle.Light);
#if UNITY_ANDROID
            Vibration.VibrateAndroid(28, 12);
#endif
        }

        /// <summary>
        /// Tile successfully routed to an order tray. Satisfying medium punch —
        /// the player should feel the "click" of a correct match.
        /// </summary>
        public void OnTileMatchedOrder()
        {
            if (!_enabled) return;
            Vibration.VibrateIOS(ImpactFeedbackStyle.Medium);
#if UNITY_ANDROID
            Vibration.VibrateAndroid(55);
#endif
        }

        /// <summary>
        /// Tile sent to the rack (no active order matched). Softer and shorter
        /// than a match — slightly deflating, not punishing.
        /// </summary>
        public void OnTileSentToRack()
        {
            if (!_enabled) return;
            Vibration.VibrateIOS(ImpactFeedbackStyle.Soft);
#if UNITY_ANDROID
            Vibration.VibrateAndroid(35);
#endif
        }

        /// <summary>
        /// An entire order is completed. Rigid, very crisp — the premium "snap"
        /// of a full set being collected.
        /// </summary>
        public void OnOrderCompleted()
        {
            if (!_enabled) return;
            Vibration.VibrateIOS(ImpactFeedbackStyle.Rigid);
#if UNITY_ANDROID
            Vibration.VibrateAndroid(70);
#endif
        }

        /// <summary>
        /// Level won. Celebratory notification-style haptic — iOS success buzz.
        /// </summary>
        public void OnLevelWon()
        {
            if (!_enabled) return;
            Vibration.VibrateIOS(NotificationFeedbackStyle.Success);
#if UNITY_ANDROID
            Vibration.VibrateAndroid(80);
#endif
        }

        /// <summary>
        /// Rack is full — game over. Heavy, unmissable impact.
        /// Communicates failure without a word.
        /// </summary>
        public void OnRackFull()
        {
            if (!_enabled) return;
            Vibration.VibrateIOS(ImpactFeedbackStyle.Heavy);
#if UNITY_ANDROID
            Vibration.VibrateAndroid(120);
#endif
        }

        /// <summary>
        /// Player tapped a blocked (non-clickable) tile. Short error nudge —
        /// informative, not harsh.
        /// </summary>
        public void OnBlockedTileTapped()
        {
            if (!_enabled) return;
            Vibration.VibrateIOS(NotificationFeedbackStyle.Warning);
#if UNITY_ANDROID
            Vibration.VibrateAndroid(40);
#endif
        }
    }
}
