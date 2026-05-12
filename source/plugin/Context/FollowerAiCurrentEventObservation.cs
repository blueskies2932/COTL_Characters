using System;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiCurrentEventObservation
    {
        private const float ObservationIntervalSeconds = 20f;
        private static float nextObservationAtRealtime;

        internal static void Update()
        {
            if (Time.realtimeSinceStartup < nextObservationAtRealtime)
                return;

            nextObservationAtRealtime = Time.realtimeSinceStartup + ObservationIntervalSeconds;

            try
            {
                FollowerAiCurrentEvents.ObserveFollowers(FollowerAiFollowerFacts.GetCurrentFollowers());
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"AI current-event observation failed: {ex.Message}");
            }
        }

        internal static void ResetForSaveScopeChange()
        {
            nextObservationAtRealtime = 0f;
        }
    }
}
