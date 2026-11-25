using System.Collections.Generic;
using MRStrategyRTS.Config;
using MRStrategyRTS.RTS;
using MRStrategyRTS.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace MRStrategyRTS.AI
{
    /// <summary>
    /// Moves this monster through a terrain path made only from one tile type.
    /// </summary>
    public sealed class MonsterPathWalker : MonoBehaviour
    {
        private const float DefaultAnimationWalkSpeed = 0.05f;

        [SerializeField] private TerrainTileGridGenerator terrain;
        private int walkableTileType = 2;
        private float moveSpeed = 0.35f;
        [SerializeField, Min(0.001f)] private float waypointArrivalDistance = 0.02f;
        [SerializeField] private bool rotateTowardsMovement = true;
        [SerializeField, Min(0f)] private float rotationSpeed = 540f;
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [Header("Events")]
        [SerializeField] private UnityEvent onDestinationReached = new();

        private readonly List<Vector3> path = new();
        private int nextWaypointIndex;
        private float walkingHeight;
        private Rigidbody body;
        private UnitCombat combat;
        private bool destinationReached;
        private bool movementPaused;
        private float animationWalkSpeed = DefaultAnimationWalkSpeed;
        private float towerMovementMultiplier = 1f;
        public float TowerMovementMultiplier => towerMovementMultiplier;

        public void SetTowerMovementMultiplier(float multiplier)
        {
            towerMovementMultiplier = Mathf.Clamp(multiplier,.25f,1f);
        }

        public event System.Action<MonsterPathWalker> DestinationReached;
        public bool MovementPaused => movementPaused;

        private void Awake()
        {
            combat = GetComponentInParent<UnitCombat>();
            if (combat == null)
            {
                combat = GetComponentInChildren<UnitCombat>(true);
            }

            if (terrain == null)
            {
                terrain = FindFirstObjectByType<TerrainTileGridGenerator>();
            }

            body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            walkingHeight = transform.position.y;
        }

        private void OnEnable()
        {
            RecalculatePath();
        }

        private void Update()
        {
            if (MRStrategyRTS.GameSessionState.IsBattlePaused) return;
            // A corpse must never advance along the path or reach the base.
            // Do not alter Animator.speed here: the death animation still needs to play.
            if (combat != null && !combat.IsAlive)
            {
                return;
            }

            if (movementPaused)
            {
                SetWalkingAnimation(false);
                return;
            }

            while (nextWaypointIndex < path.Count)
            {
                var reachedWaypoint = path[nextWaypointIndex];
                reachedWaypoint.y = walkingHeight;
                var offsetToWaypoint = reachedWaypoint - transform.position;
                offsetToWaypoint.y = 0f;
                if (offsetToWaypoint.sqrMagnitude > waypointArrivalDistance * waypointArrivalDistance)
                {
                    break;
                }

                transform.position = reachedWaypoint;
                nextWaypointIndex++;
            }

            if (nextWaypointIndex >= path.Count)
            {
                SetWalkingAnimation(false);
                NotifyDestinationReached();
                return;
            }

            var waypoint = path[nextWaypointIndex];
            waypoint.y = walkingHeight;
            var horizontalOffset = waypoint - transform.position;
            horizontalOffset.y = 0f;

            if (rotateTowardsMovement)
            {
                var targetRotation = Quaternion.LookRotation(horizontalOffset, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime);
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                waypoint,
                moveSpeed * towerMovementMultiplier * Time.deltaTime);
            SetWalkingAnimation(true);
        }

        [ContextMenu("Walk To Destination")]
        public void RecalculatePath()
        {
            path.Clear();
            nextWaypointIndex = 0;
            destinationReached = false;
            walkingHeight = transform.position.y;

            if (terrain == null ||
                !terrain.TryGetPathMarkers(out _, out var destination))
            {
                Debug.LogWarning(
                    "MonsterPathWalker needs a Terrain Tile Grid Generator plus Origin and Destination tagged objects.",
                    this);
                return;
            }

            if (!terrain.TryFindTilePath(
                    transform.position,
                    destination.position,
                    walkableTileType,
                    path))
            {
                Debug.LogWarning(
                    $"MonsterPathWalker could not find a path using tile type {walkableTileType:D2}.",
                    this);
                SetWalkingAnimation(false);
                return;
            }

            RemoveStraightLineWaypoints();
        }

        public void SetPath(TerrainTileGridGenerator pathTerrain)
        {
            if (pathTerrain != null)
            {
                terrain = pathTerrain;
            }

            RecalculatePath();
        }

        public void ApplyDefinition(MonsterDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            walkableTileType = definition.walkableTileType;
            moveSpeed = definition.moveSpeed;
            animationWalkSpeed = definition.animationWalkSpeed;
        }

        public void SetMovementPaused(bool paused)
        {
            movementPaused = paused;
            if (paused)
            {
                SetWalkingAnimation(false);
            }
        }

        private void NotifyDestinationReached()
        {
            if (destinationReached || path.Count == 0)
            {
                return;
            }

            destinationReached = true;
            onDestinationReached.Invoke();
            DestinationReached?.Invoke(this);
        }

        private void RemoveStraightLineWaypoints()
        {
            if (path.Count < 3)
            {
                return;
            }

            var simplifiedPath = new List<Vector3> { path[0] };
            for (var index = 1; index < path.Count - 1; index++)
            {
                var previous = simplifiedPath[simplifiedPath.Count - 1];
                var current = path[index];
                var next = path[index + 1];
                var directionToCurrent = current - previous;
                var directionToNext = next - current;
                directionToCurrent.y = 0f;
                directionToNext.y = 0f;
                if (Vector3.Cross(directionToCurrent.normalized, directionToNext.normalized).sqrMagnitude
                    > 0.0001f)
                {
                    simplifiedPath.Add(current);
                }
            }

            simplifiedPath.Add(path[path.Count - 1]);
            path.Clear();
            path.AddRange(simplifiedPath);
        }

        private void SetWalkingAnimation(bool isWalking)
        {
            if (animator == null || IsPlayingCombatReaction())
            {
                return;
            }

            animator.speed = isWalking ? moveSpeed * towerMovementMultiplier / animationWalkSpeed : 0f;
        }

        private bool IsPlayingCombatReaction()
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type != AnimatorControllerParameterType.Bool
                    || (parameter.name != "Die" && parameter.name != "Injured"))
                {
                    continue;
                }

                if (animator.GetBool(parameter.name))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
