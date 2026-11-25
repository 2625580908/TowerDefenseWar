using MRStrategyRTS.Shared;
using UnityEngine;

namespace MRStrategyRTS.RTS
{
    public sealed class RTSFactionEconomy : MonoBehaviour
    {
        [SerializeField] private FactionId faction;
        [SerializeField, Min(0)] private int credits = 2500;
        [SerializeField, Min(1)] private int supplyLimit = 20;

        public FactionId Faction => faction;
        public int Credits => credits;
        public int SupplyUsed { get; private set; }
        public int SupplyLimit => supplyLimit;

        public bool CanAfford(UnitDefinition definition) =>
            definition != null &&
            definition.faction == faction &&
            credits >= definition.credits &&
            SupplyUsed + definition.supply <= supplyLimit;

        public bool TrySpend(UnitDefinition definition)
        {
            if (!CanAfford(definition))
            {
                return false;
            }

            credits -= definition.credits;
            SupplyUsed += definition.supply;
            return true;
        }

        public void AddCredits(int amount) => credits = Mathf.Max(0, credits + amount);
        public void SetSupplyLimit(int value) => supplyLimit = Mathf.Max(1, value);
    }
}
