using System.Collections;
using MRStrategyRTS.Shared;
using UnityEngine;

namespace MRStrategyRTS.RTS
{
    public sealed class UnitProductionBuilding : MonoBehaviour
    {
        [SerializeField] private FactionId faction;
        [SerializeField] private RTSFactionEconomy economy;
        [SerializeField] private Transform spawnPoint;

        private bool isProducing;
        public bool IsProducing => isProducing;

        public bool TryProduce(UnitDefinition definition)
        {
            if (isProducing || definition == null || definition.prefab == null || definition.faction != faction)
            {
                return false;
            }

            if (economy == null || !economy.TrySpend(definition))
            {
                return false;
            }

            StartCoroutine(ProduceRoutine(definition));
            return true;
        }

        private IEnumerator ProduceRoutine(UnitDefinition definition)
        {
            isProducing = true;
            yield return new WaitForSeconds(definition.productionSeconds);
            Instantiate(definition.prefab, spawnPoint.position, spawnPoint.rotation);
            isProducing = false;
        }
    }
}
