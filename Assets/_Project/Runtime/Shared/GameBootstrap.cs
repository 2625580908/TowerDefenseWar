using MRStrategyRTS.RTS;
using UnityEngine;

namespace MRStrategyRTS.Shared
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private RTSFactionEconomy allianceEconomy;
        [SerializeField] private RTSFactionEconomy legionEconomy;

        public RTSFactionEconomy AllianceEconomy => allianceEconomy;
        public RTSFactionEconomy LegionEconomy => legionEconomy;

        private void Awake()
        {
            Application.targetFrameRate = 72;
            QualitySettings.vSyncCount = 0;
        }
    }
}
