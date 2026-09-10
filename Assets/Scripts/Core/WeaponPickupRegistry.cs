using System.Collections.Generic;

namespace IsometricShooter.Core
{
    public sealed class WeaponPickupRegistry
    {
        private readonly Dictionary<string, ItemPickup> pickups = new Dictionary<string, ItemPickup>();

        public void Register(string uniqueId, ItemPickup pickup)
        {
            if (string.IsNullOrEmpty(uniqueId) || pickup == null)
                return;

            pickups[uniqueId] = pickup;
        }

        public bool TryGet(string uniqueId, out ItemPickup pickup)
        {
            pickup = null;

            if (string.IsNullOrEmpty(uniqueId))
                return false;

            return pickups.TryGetValue(uniqueId, out pickup) && pickup != null;
        }

        public void Remove(string uniqueId)
        {
            pickups.Remove(uniqueId);
        }
    }
}