using System.Collections.Generic;
using UnityEngine;

namespace MRStrategyRTS.RTS
{
    public sealed class RTSSquad : MonoBehaviour
    {
        private readonly List<RTSUnit> units = new();

        public IReadOnlyList<RTSUnit> Units => units;

        private void Awake()
        {
            RefreshUnits();
        }

        public void RefreshUnits()
        {
            units.Clear();
            GetComponentsInChildren(units);
        }
    }
}
