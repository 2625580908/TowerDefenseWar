using UnityEngine;
using MRStrategyRTS.Config;

namespace MRStrategyRTS.RTS
{
    /// <summary>
    /// Simple visible projectile used by UnitCombat.
    /// </summary>
    public sealed class UnitProjectile : MonoBehaviour
    {
        private UnitCombat target;
        private int damage;
        private float speed;
        private bool launched;
        private GameObject owner;
        private ProjectileParticleEffects particleEffects;
        private float expiresAt;
        private TowerDefinition towerDefinition;
        private string enemyTag;

        public void Launch(
            UnitCombat targetUnit,
            int projectileDamage,
            float projectileSpeed,
            GameObject projectileOwner = null,
            TowerDefinition definition = null,
            string targetTag = "Enemy")
        {
            target = targetUnit;
            damage = projectileDamage;
            speed = projectileSpeed;
            owner = projectileOwner;
            towerDefinition = definition;
            enemyTag = targetTag;
            launched = true;
            expiresAt = Time.time + 3f;
            particleEffects = GetComponent<ProjectileParticleEffects>();
            FaceParticleTravelDirection();
            particleEffects?.Begin();
        }

        private void Update()
        {
            if (MRStrategyRTS.GameSessionState.IsBattlePaused) return;
            if (!launched)
            {
                // This component must only run on projectiles created by UnitCombat.
                // Disabling protects a unit if the script is accidentally attached to it.
                enabled = false;
                return;
            }

            if (Time.time >= expiresAt || target == null || !target.IsAlive)
            {
                particleEffects?.Complete(false);
                Destroy(gameObject);
                return;
            }

            var targetPosition = target.transform.position + Vector3.up * 0.03f;
            FaceParticleTravelDirection();
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                speed * Time.deltaTime);

            if ((transform.position - targetPosition).sqrMagnitude > 0.000025f)
            {
                return;
            }

            particleEffects?.Complete(true);
            TowerImpactAudio.Play(towerDefinition,transform.position);
            if (towerDefinition == null || towerDefinition.damageMode == TowerDamageMode.Single)
                target.TakeDamage(damage,owner);
            else
                new TowerDamageResolver().Resolve(target,towerDefinition,damage,owner,enemyTag);
            Destroy(gameObject);
        }

        private void FaceParticleTravelDirection()
        {
            if (particleEffects == null || target == null)
                return;
            var direction = target.transform.position + Vector3.up * .03f - transform.position;
            if (direction.sqrMagnitude > .000001f)
                transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
