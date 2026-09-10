using UnityEngine;

namespace IsometricShooter.Core
{
    public static class ReloadCalculator
    {
        public struct ReloadPlan
        {
            public int magazineAmmo;
            public int reserveAmmo;
        }

        public static bool CanReload(int magazineAmmo, int reserveAmmo, int magazineSize)
        {
            return magazineSize > 0 && magazineAmmo < magazineSize && reserveAmmo > 0;
        }

        public static ReloadPlan Resolve(int magazineAmmo, int reserveAmmo, int magazineSize)
        {
            int needed = Mathf.Max(0, magazineSize - magazineAmmo);
            int toLoad = Mathf.Min(needed, reserveAmmo);

            return new ReloadPlan
            {
                magazineAmmo = magazineAmmo + toLoad,
                reserveAmmo = reserveAmmo - toLoad
            };
        }
    }
}