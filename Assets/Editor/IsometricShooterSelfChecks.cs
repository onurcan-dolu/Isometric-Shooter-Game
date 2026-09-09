using UnityEngine;
using UnityEditor;

namespace IsometricShooter.Editor
{
    /// <summary>
    /// Editor-only smoke tests for the extracted, pure-logic collaborators
    /// (reload math, drop launch math, ammo persistence, pickup registry).
    /// Run from "Isometric Shooter/Run Self Checks". No scene or network needed.
    /// </summary>
    public static class IsometricShooterSelfChecks
    {
        private static int failures;

        [MenuItem("Isometric Shooter/Run Self Checks")]
        public static void RunAll()
        {
            failures = 0;

            TestReloadCalculator();
            TestDropCalculator();
            TestAmmoStore();
            TestPickupRegistry();
            TestReloadRuntimeCancel();

            Debug.Log(failures == 0
                ? "[SelfCheck] ALL PASSED"
                : "[SelfCheck] " + failures + " check(s) FAILED");
        }

        private static void Check(string name, bool condition, string detail = "")
        {
            if (condition)
                return;

            failures++;
            Debug.LogError("[SelfCheck] FAILED: " + name + (detail.Length > 0 ? " :: " + detail : ""));
        }

        private static void TestReloadCalculator()
        {
            Check("CanReload empty mag with reserve", ReloadCalculator.CanReload(0, 90, 30));
            Check("CanReload partial mag with reserve", ReloadCalculator.CanReload(5, 20, 30));
            Check("CanReload full mag rejected", !ReloadCalculator.CanReload(30, 10, 30));
            Check("CanReload empty reserve rejected", !ReloadCalculator.CanReload(5, 0, 30));

            ReloadCalculator.ReloadPlan plan = ReloadCalculator.Resolve(5, 20, 30);
            Check("Resolve fills magazine from reserve",
                plan.magazineAmmo == 25 && plan.reserveAmmo == 0,
                plan.magazineAmmo + "/" + plan.reserveAmmo);

            plan = ReloadCalculator.Resolve(0, 90, 30);
            Check("Resolve full reload",
                plan.magazineAmmo == 30 && plan.reserveAmmo == 60,
                plan.magazineAmmo + "/" + plan.reserveAmmo);

            plan = ReloadCalculator.Resolve(5, 5, 30);
            Check("Resolve never overfills magazine",
                plan.magazineAmmo == 10 && plan.reserveAmmo == 0,
                plan.magazineAmmo + "/" + plan.reserveAmmo);

            plan = ReloadCalculator.Resolve(30, 10, 30);
            Check("Resolve full magazine is no-op",
                plan.magazineAmmo == 30 && plan.reserveAmmo == 10,
                plan.magazineAmmo + "/" + plan.reserveAmmo);
        }

        private static void TestDropCalculator()
        {
            GameObject owner = new GameObject("SelfCheckDrop");
            try
            {
                owner.transform.position = new Vector3(10f, 2f, -5f);
                owner.transform.rotation = Quaternion.identity;

                Vector3 spawn = WeaponDropCalculator.ComputeSpawnPosition(owner.transform, 0.6f, 1.2f);
                Check("Spawn position applies height and forward offsets",
                    spawn.x == 10f && Mathf.Approximately(spawn.y, 2.6f) && spawn.z == -3.8f,
                    spawn.ToString());

                Vector3 velocity = WeaponDropCalculator.ComputeDropVelocity(owner.transform, 1.8f);
                float expectedUp = Mathf.Sqrt(2f * 1.8f * 9.81f);
                Check("Drop velocity forward speed is 2 m/s",
                    Mathf.Approximately(velocity.z, 2f),
                    velocity.ToString());
                Check("Drop velocity upward speed matches throw height",
                    Mathf.Approximately(velocity.y, expectedUp),
                    velocity.ToString());
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void TestAmmoStore()
        {
            WeaponAmmoStore store = new WeaponAmmoStore();

            store.Save("slot-a", 13, 44);
            Check("AmmoStore restores saved ammo",
                store.TryRestore("slot-a", out int current, out int reserve) && current == 13 && reserve == 44,
                current + "/" + reserve);

            store.Save("slot-a", 7, 99);
            Check("AmmoStore overwrites existing entry",
                store.TryRestore("slot-a", out current, out reserve) && current == 7 && reserve == 99,
                current + "/" + reserve);

            store.Remove("slot-a");
            Check("AmmoStore remove drops entry",
                !store.TryRestore("slot-a", out current, out reserve));

            Check("AmmoStore rejects empty key",
                !store.TryRestore(string.Empty, out current, out reserve));
        }

        private static void TestPickupRegistry()
        {
            WeaponPickupRegistry registry = new WeaponPickupRegistry();

            Check("PickupRegistry empty lookup", !registry.TryGet("missing", out ItemPickup pickup));

            registry.Register("slot-a", null);
            Check("PickupRegistry ignores null pickup",
                !registry.TryGet("slot-a", out pickup));

            registry.Remove("slot-a");
            Check("PickupRegistry remove is safe",
                !registry.TryGet("slot-a", out pickup));
        }

        private static void TestReloadRuntimeCancel()
        {
            ReloadRuntime runtime = new ReloadRuntime();

            Check("ReloadRuntime inactive initially", !runtime.IsActive);

            runtime.Begin(1f);
            Check("ReloadRuntime active after begin", runtime.IsActive);

            runtime.Cancel();
            Check("ReloadRuntime inactive after cancel", !runtime.IsActive);
            Check("ReloadRuntime tick after cancel", !runtime.Tick());

            runtime.Begin(1f);
            Check("ReloadRuntime can restart", runtime.IsActive);
            runtime.Cancel();
        }
    }
}