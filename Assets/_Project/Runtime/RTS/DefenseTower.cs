using UnityEngine;
using MRStrategyRTS.Config;
using MRStrategyRTS.Input;
using MRStrategyRTS.Shared;

namespace MRStrategyRTS.RTS
{
    /// <summary>
    /// Automatically attacks the nearest alive enemy within range.
    /// </summary>
    public sealed class DefenseTower : MonoBehaviour
    {
        private readonly TowerTargetQuery targetQuery = new();
        private readonly System.Collections.Generic.List<UnitCombat> targetBuffer = new();

        [Header("Tower Definition")]
        [Tooltip("指定后，运行时攻击数值和子弹预制体由此配置驱动。")]
        [SerializeField] private TowerDefinition towerDefinition;

        [Header("Projectile")]
        private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        private int damage;
        private float bulletSpeed;
        [Tooltip("地图缩放后，子弹模型使用相同缩放倍率。")]
        [SerializeField] private bool scaleProjectileWithTerrain = true;

        [Header("Audio")]
        [Tooltip("所有炮台共用播放时间窗口，同期发射只播放一次，避免多炮台声音叠加。")]
        public AudioClip shootSound;
        [Range(0f, 1f)] public float shootSoundVolume = 1f;

        // One shared gate across every tower, regardless of prefab or audio clip.
        private const double MinimumShootSoundInterval = 0.12;
        private static double nextShootSoundAt = double.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShootSoundGate()
        {
            nextShootSoundAt = double.NegativeInfinity;
        }

        [Header("Targeting")]
        [SerializeField] private string enemyTag = "Enemy";
        private float attackRange;
        private float attackInterval;
        [SerializeField, Min(1f)] private float turnSpeed = 540f;
        [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.15f;

        [Header("攻击范围可视化")]
        [Tooltip("仅在 Unity Scene 视图显示与实际 Attack Range 相同半径的圆环。")]
        [SerializeField] private bool showAttackRange;
        [SerializeField, Range(12, 128)] private int rangeCircleSegments = 48;
        [SerializeField, Range(1f, 10f)] private float rangeLineWidth = 2f;
        [SerializeField] private float rangeCircleHeightOffset = 0.01f;
        [SerializeField] private Color rangeCircleColor = new(0.2f, 0.9f, 1f, 0.8f);

        private UnitCombat target;
        private float nextAttackAt;
        private float nextTargetRefreshAt;
        private Quaternion baseLocalRotation;
        private AudioSource audioSource;
        private TerrainTwoHandPinchManipulator terrainManipulator;

        public TowerDefinition TowerDefinition => towerDefinition;
        public int BuildCost => towerDefinition != null ? towerDefinition.buildCost : 0;
        public float AttackRange => towerDefinition != null ? towerDefinition.attackRange : attackRange;

        private void Awake()
        {
            ApplyDefinition(towerDefinition);
            audioSource = GetComponent<AudioSource>();
            ResolveTerrainManipulator();
            ResetAimOrientation();
        }

        private void OnEnable()
        {
            ResolveTerrainManipulator();
            ResetAimOrientation();
        }

        public void ApplyDefinition(TowerDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            towerDefinition = definition;
            bulletPrefab = definition.bulletPrefab;
            damage = definition.damage;
            bulletSpeed = definition.bulletSpeed;
            attackRange = definition.attackRange;
            attackInterval = definition.attackInterval;
            var pokeInfo = GetComponent<DefenseTowerPokeInfo>();
            if (pokeInfo != null) pokeInfo.Initialize(definition);
            else Debug.LogError("防御塔预制体未绑定 DefenseTowerPokeInfo 和参数 UI。", this);
        }

        private void ResolveTerrainManipulator()
        {
            terrainManipulator = GetComponentInParent<TerrainTwoHandPinchManipulator>();
            if (terrainManipulator == null)
            {
                var terrain = GetComponentInParent<TerrainTileGridGenerator>();
                terrainManipulator = terrain != null
                    ? terrain.GetComponent<TerrainTwoHandPinchManipulator>()
                    : null;
            }
        }

        public void ResetAimOrientation()
        {
            var localEulerAngles = transform.localEulerAngles;
            baseLocalRotation = Quaternion.Euler(
                localEulerAngles.x,
                localEulerAngles.y,
                0f);
            target = null;
            nextAttackAt = Time.time;
            nextTargetRefreshAt = Time.time;
        }

        private void Update()
        {
            if (MRStrategyRTS.GameSessionState.IsBattlePaused) return;
            if (Time.time >= nextTargetRefreshAt)
            {
                nextTargetRefreshAt = Time.time + targetRefreshInterval;
                target = FindNearestEnemy();
            }

            if (target == null || !target.IsAlive)
            {
                return;
            }

            var targetOffset = target.transform.position - transform.position;
            if (targetOffset.sqrMagnitude > attackRange * attackRange)
            {
                target = null;
                return;
            }

            if (targetOffset.sqrMagnitude > 0.000001f)
            {
                RotateLocalZTowards(targetOffset);
            }

            if (Time.time < nextAttackAt)
            {
                return;
            }

            Fire(target);
            nextAttackAt = Time.time + attackInterval;
        }

        private UnitCombat FindNearestEnemy()
        {
            targetQuery.Collect(transform.position,attackRange,enemyTag,targetBuffer);
            UnitCombat nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var candidate in targetBuffer)
            {
                var distance = (candidate.transform.position - transform.position).sqrMagnitude;
                var priority = towerDefinition != null ? towerDefinition.targetPriority : TowerTargetPriority.Nearest;
                var score = priority == TowerTargetPriority.HighestHealth ? -candidate.Health : 0;
                if (priority == TowerTargetPriority.Unpoisoned
                    && candidate.TryGetComponent<UnitTowerStatus>(out var status) && status.IsPoisoned)
                    score = 1;
                var bestScore = nearest == null ? int.MaxValue :
                    priority == TowerTargetPriority.HighestHealth ? -nearest.Health :
                    priority == TowerTargetPriority.Unpoisoned
                    && nearest.TryGetComponent<UnitTowerStatus>(out var bestStatus) && bestStatus.IsPoisoned ? 1 : 0;
                if (score < bestScore || score == bestScore && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private void Fire(UnitCombat targetUnit)
        {
            if (bulletPrefab == null)
            {
                Debug.LogWarning("DefenseTower requires a Bullet Prefab.", this);
                return;
            }

            var spawnTransform = firePoint != null ? firePoint : transform;
            var projectile = Instantiate(
                bulletPrefab,
                spawnTransform.position,
                Quaternion.identity);
            projectile.name = $"{name} Bullet";
            if (scaleProjectileWithTerrain && terrainManipulator != null)
            {
                projectile.transform.localScale *= terrainManipulator.CurrentScaleMultiplier;
            }

            var projectileController = projectile.GetComponent<UnitProjectile>();
            if (projectileController == null)
            {
                projectileController = projectile.AddComponent<UnitProjectile>();
            }

            projectileController.Launch(targetUnit, damage, bulletSpeed, gameObject, towerDefinition, enemyTag);
            PlayShootSound();
        }

        private void RotateLocalZTowards(Vector3 targetOffset)
        {
            var parentRotation = transform.parent != null
                ? transform.parent.rotation
                : Quaternion.identity;
            var rotationFrame = parentRotation * baseLocalRotation;
            var localZAxis = rotationFrame * Vector3.forward;
            var localYForward = rotationFrame * Vector3.up;
            var directionOnRotationPlane = Vector3.ProjectOnPlane(targetOffset, localZAxis);
            if (directionOnRotationPlane.sqrMagnitude < 0.000001f)
            {
                return;
            }

            var desiredZAngle = Vector3.SignedAngle(
                localYForward,
                directionOnRotationPlane,
                localZAxis);
            var targetLocalRotation = baseLocalRotation
                * Quaternion.AngleAxis(desiredZAngle, Vector3.forward);
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                targetLocalRotation,
                turnSpeed * Time.deltaTime);
        }

        private void PlayShootSound()
        {
            var hasConfiguredSound = towerDefinition != null && towerDefinition.shootSound != null;
            var clip = hasConfiguredSound ? towerDefinition.shootSound : shootSound;
            var volume = hasConfiguredSound ? towerDefinition.shootSoundVolume : shootSoundVolume;
            if (clip == null || volume <= 0f)
            {
                return;
            }

            var now = Time.unscaledTimeAsDouble;
            if (now < nextShootSoundAt)
            {
                return;
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }

            if (!audioSource.isActiveAndEnabled || audioSource.volume <= 0f)
            {
                return;
            }

            // Keep long clips from overlapping even when shots straddle two windows.
            var clipDuration = clip.length / Mathf.Max(0.01f, Mathf.Abs(audioSource.pitch));
            nextShootSoundAt = now + System.Math.Max(MinimumShootSoundInterval, clipDuration);
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
