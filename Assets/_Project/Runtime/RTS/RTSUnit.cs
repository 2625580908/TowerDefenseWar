using MRStrategyRTS.Shared;
using UnityEngine;
using UnityEngine.AI;

namespace MRStrategyRTS.RTS
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class RTSUnit : MonoBehaviour
    {
        public enum UnitState { Idle, Move, Attack, Dead }

        [SerializeField] private UnitDefinition definition;
        [SerializeField] private Transform selectionAnchor;

        private NavMeshAgent agent;
        private RTSUnit target;
        private float nextAttackTime;
        private int health;

        public UnitDefinition Definition => definition;
        public FactionId Faction => definition != null ? definition.faction : FactionId.Neutral;
        public UnitState State { get; private set; }
        public bool IsAlive => State != UnitState.Dead;
        public Transform SelectionAnchor => selectionAnchor != null ? selectionAnchor : transform;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = definition != null ? definition.maxHealth : 1;
            if (definition != null)
            {
                agent.speed = definition.moveSpeed;
            }
        }

        private void Update()
        {
            if (!IsAlive || target == null || !target.IsAlive)
            {
                return;
            }

            var targetPosition = target.transform.position;
            var distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > definition.attackRange)
            {
                agent.isStopped = false;
                agent.SetDestination(targetPosition);
                State = UnitState.Attack;
                return;
            }

            agent.isStopped = true;
            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + definition.attackCooldown;
                target.TakeDamage(definition.attackDamage);
            }
        }

        public void MoveTo(Vector3 worldPosition)
        {
            if (!IsAlive)
            {
                return;
            }

            target = null;
            agent.isStopped = false;
            agent.SetDestination(worldPosition);
            State = UnitState.Move;
        }

        public void Attack(RTSUnit enemy)
        {
            if (!IsAlive || enemy == null || enemy.Faction == Faction)
            {
                return;
            }

            target = enemy;
            State = UnitState.Attack;
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive)
            {
                return;
            }

            health -= Mathf.Max(0, damage);
            if (health > 0)
            {
                return;
            }

            State = UnitState.Dead;
            agent.isStopped = true;
            gameObject.SetActive(false);
        }
    }
}
