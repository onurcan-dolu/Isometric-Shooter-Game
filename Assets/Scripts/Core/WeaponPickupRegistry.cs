using System.Collections.Generic;

namespace IsometricShooter.Core
{
    /// <summary>
    /// Maps an inventory slot's uniqueId to the world ItemPickup that represents
    /// the same item in the scene (e.g. a weapon that was picked up stays as a
    /// physical pickup elsewhere in the world and can be re-dropped).
    /// </summary>
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