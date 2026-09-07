namespace IsometricShooter.Core
{
    public interface IDamageable
    {
        void TakeDamage(float damage, UnityEngine.Vector3 hitDirection, UnityEngine.Vector3 hitPosition, float impactForce);
        float GetHealth();
        float GetMaxHealth();
        bool IsDead();
    }
}
