using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

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
            // <uses-permission android:name=\"android.permission.VIBRATE\"/> 
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
            TriggerAndroidPredefinedEffect(2, 10);
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
            // 2 = EFFECT_TICK
            TriggerAndroidPredefinedEffect(2, 10);
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
            // 2 = EFFECT_TICK (very light, crisp tick)
            TriggerAndroidPredefinedEffect(2, 10);
#endif
        }

        /// <summary>
        /// An entire order is completed. Rigid, very crisp — the premium "snap"
        /// of a full set being collected.
        /// </summary>
        public void OnOrderCompleted(CancellationToken ct = default)
        {
            if (!_enabled) return;
            Vibration.VibrateIOS(ImpactFeedbackStyle.Rigid);
#if UNITY_ANDROID
            PlayOrderCompletedSequenceAsync(ct).Forget();
#endif
        }

        /// <summary>
        /// Level won. Celebratory notification-style haptic — iOS success buzz.
        /// </summary>
        public void OnLevelWon(CancellationToken ct = default)
        {
            if (!_enabled) return;
            Vibration.VibrateIOS(NotificationFeedbackStyle.Success);
#if UNITY_ANDROID
            PlayWinTickSequenceAsync(ct).Forget();
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

        /// <summary>
        /// General UI button tapped. Crisp tick for menu interaction.
        /// </summary>
        public void OnUIButtonTapped()
        {
            if (!_enabled) return;
            Vibration.VibrateIOS(ImpactFeedbackStyle.Light);
#if UNITY_ANDROID
            TriggerAndroidPredefinedEffect(2, 10);
#endif
        }

#if UNITY_ANDROID
        private async Cysharp.Threading.Tasks.UniTaskVoid PlayOrderCompletedSequenceAsync(CancellationToken ct)
        {
            // 5 = EFFECT_HEAVY_CLICK
            TriggerAndroidPredefinedEffect(5, 30);
            await Cysharp.Threading.Tasks.UniTask.Delay(80, cancellationToken: ct).SuppressCancellationThrow();
            if (ct.IsCancellationRequested) return;
            // 2 = EFFECT_TICK
            TriggerAndroidPredefinedEffect(2, 10);
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid PlayWinTickSequenceAsync(CancellationToken ct)
        {
            // Sequence of 10 ticks with 40ms delay between them (400ms total)
            for (int i = 0; i < 10; i++)
            {
                TriggerAndroidPredefinedEffect(2, 10); // tick
                await Cysharp.Threading.Tasks.UniTask.Delay(40, cancellationToken: ct).SuppressCancellationThrow();
                if (ct.IsCancellationRequested) return;
            }

            // Wait an additional 160ms to sync the final hit precisely at 0.6s with the VisualFeedbackFinishedSignal
            await Cysharp.Threading.Tasks.UniTask.Delay(160, cancellationToken: ct).SuppressCancellationThrow();
            if (ct.IsCancellationRequested) return;

            // 11th hit perfectly synced with the Rack Drop / Win Screen
            TriggerAndroidPredefinedEffect(1, 30); // 1 = EFFECT_DOUBLE_CLICK
        }

        /// <summary>
        /// Attempts to trigger a hardware-level Predefined Effect (like EFFECT_TICK = 2) on API 29+.
        /// Falls back to a standard millisecond duration if the API is too old.
        /// </summary>
        private void TriggerAndroidPredefinedEffect(int effectId, long fallbackMilliseconds)
        {
            if (Vibration.AndroidVersion >= 29 && Vibration.vibrationEffect != null && Vibration.vibrator != null)
            {
                try
                {
                    UnityEngine.AndroidJavaObject effect = Vibration.vibrationEffect.CallStatic<UnityEngine.AndroidJavaObject>("createPredefined", effectId);
                    Vibration.vibrator.Call("vibrate", effect);
                    return; // Successfully triggered the hardware effect
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[HapticService] Failed to play predefined effect {effectId}: {e.Message}");
                }
            }
            
            // Fallback for API < 29 or if it throws an error
            Vibration.VibrateAndroid(fallbackMilliseconds);
        }
#endif
    }
}
