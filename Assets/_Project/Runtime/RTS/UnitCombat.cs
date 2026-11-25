using MRStrategyRTS.Config;
using MRStrategyRTS.AI;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MRStrategyRTS.RTS
{
    /// <summary>
    /// Lightweight tabletop combat component. Attach it to a unit with a Collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class UnitCombat : MonoBehaviour
    {
        private int maxHealth = 100;

        private int killGoldReward = 10;

        [Header("血条界面")]
        [Tooltip("血条前景 Image，Image Type 设为 Filled。")]
        [FormerlySerializedAs("healthScrollbar")]
        public Image healthFillImage;

        [Header("受伤与死亡动画")]
        [SerializeField] private Animator unitAnimator;
        [SerializeField, Min(0.05f)] private float injuredAnimationDuration = 1.5f;
        [SerializeField, Min(0.05f)] private float injuredAnimationCooldown = 2.5f;
        [SerializeField, Min(0f)] private float deathDestroyDelay = 2f;

        private Rigidbody unitRigidbody;
        private int health;
        private float injuredResetAt = -1f;
        private float nextInjuredAnimationAt;
        private bool hasDieParameter;
        private bool hasInjuredParameter;

        public bool IsAlive => health > 0;
        public int Health => health;
        public int MaxHealth => maxHealth;
        public int KillGoldReward => killGoldReward;
        public event System.Action<UnitCombat> Died;
        public event System.Action<UnitCombat, GameObject> KilledBy;

        private void Awake()
        {
            unitRigidbody = GetComponent<Rigidbody>();
            if (unitRigidbody != null)
            {
                unitRigidbody.isKinematic = true;
                unitRigidbody.useGravity = false;
                unitRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }

            if (unitAnimator == null)
            {
                unitAnimator = GetComponentInChildren<Animator>(true);
            }
            hasDieParameter = HasBoolParameter("Die");
            hasInjuredParameter = HasBoolParameter("Injured");
            health = maxHealth;
            UpdateHealthBar();
        }

        private void Update()
        {
            if (MRStrategyRTS.GameSessionState.IsBattlePaused) return;
            if (injuredResetAt >= 0f && Time.time >= injuredResetAt)
            {
                unitAnimator.SetBool("Injured", false);
                injuredResetAt = -1f;
            }
        }

        public void TakeDamage(int damage)
        {
            TakeDamage(damage, null);
        }

        public void ApplyDefinition(MonsterDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            maxHealth = definition.maxHealth;
            killGoldReward = definition.killGoldReward;
            health = maxHealth;
            UpdateHealthBar();
        }

        public void TakeDamage(int damage, GameObject attacker)
        {
            if (MRStrategyRTS.GameSessionState.IsBattlePaused) return;
            if (!IsAlive)
            {
                return;
            }

            health = Mathf.Max(0, health - Mathf.Max(0, damage));
            UpdateHealthBar();
            Debug.Log("[UnitCombat] " + name + " health: " + health + "/" + maxHealth, this);
            if (health == 0)
            {
                PlayDeathAnimation();
                KilledBy?.Invoke(this, attacker);
                Died?.Invoke(this);
                return;
            }

            PlayInjuredAnimation();
        }

        private void UpdateHealthBar()
        {
            if (healthFillImage != null)
            {
                var ratio = maxHealth == 0 ? 0f : (float)health / maxHealth;
                healthFillImage.fillAmount = ratio;
            }
        }

        private void PlayInjuredAnimation()
        {
            if (!hasInjuredParameter || Time.time < nextInjuredAnimationAt)
            {
                return;
            }

            unitAnimator.speed = 1f;
            unitAnimator.SetBool("Injured", true);
            injuredResetAt = Time.time + injuredAnimationDuration;
            nextInjuredAnimationAt = Time.time + injuredAnimationCooldown;
        }

        private void PlayDeathAnimation()
        {
            // Stop path movement before callbacks or the delayed corpse cleanup.
            foreach (var walker in GetComponentsInChildren<MonsterPathWalker>(true))
            {
                walker.SetMovementPaused(true);
            }

            if (unitAnimator != null)
            {
                // Keep the death animation playing without moving the unit root.
                unitAnimator.applyRootMotion = false;
            }

            if (hasInjuredParameter)
            {
                unitAnimator.SetBool("Injured", false);
            }

            if (hasDieParameter)
            {
                unitAnimator.speed = 1f;
                unitAnimator.SetBool("Die", true);
            }

            Destroy(gameObject, deathDestroyDelay);
        }

        private bool HasBoolParameter(string parameterName)
        {
            if (unitAnimator == null)
            {
                return false;
            }

            foreach (var parameter in unitAnimator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool
                    && parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
