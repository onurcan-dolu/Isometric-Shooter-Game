using UnityEngine;

namespace IsometricShooter.Core
{
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