using System.Collections.Generic;

namespace IsometricShooter.Core
{
    /// <summary>
    /// Per-inventory-item ammo persistence. Magazine/reserve ammo is keyed by the
    /// inventory slot's uniqueId so switching weapons preserves their ammo.
    /// </summary>
    public sealed class WeaponAmmoStore
    {
        private struct AmmoEntry
        {
            public int current;
            public int reserve;
        }

        private readonly Dictionary<string, AmmoEntry> states = new Dictionary<string, AmmoEntry>();

        public void Save(string uniqueId, int current, int reserve)
        {
            if (string.IsNullOrEmpty(uniqueId))
                return;

            states[uniqueId] = new AmmoEntry { current = current, reserve = reserve };
        }

        public bool TryRestore(string uniqueId, out int current, out int reserve)
        {
            current = 0;
            reserve = 0;

            if (string.IsNullOrEmpty(uniqueId))
                return false;

            if (states.TryGetValue(uniqueId, out AmmoEntry entry))
            {
                current = entry.current;
                reserve = entry.reserve;
                return true;
            }

            return false;
        }

        public void Remove(string uniqueId)
        {
            states.Remove(uniqueId);
        }
    }
}