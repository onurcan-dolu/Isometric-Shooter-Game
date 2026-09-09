using UnityEngine;

namespace IsometricShooter.Core
{
    /// <summary>
    /// Frame-advanced reload timer. Replaces a coroutine on the controller so the
    /// reload flow stays small and testable.
    /// </summary>
    public sealed class ReloadRuntime
    {
        private float finishTime;
        private bool active;

        public bool IsActive => active;

        public void Begin(float reloadTime)
        {
            active = true;
            finishTime = Time.time + reloadTime;
        }

        public void Cancel()
        {
            active = false;
        }

        /// <summary>Advances the timer; returns true once the reload has finished.</summary>
        public bool Tick()
        {
            if (!active)
                return false;

            if (Time.time < finishTime)
                return false;

            active = false;
            return true;
        }
    }
}