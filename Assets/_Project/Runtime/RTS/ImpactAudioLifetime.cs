using UnityEngine;

namespace MRStrategyRTS.RTS
{
    /// <summary>Cleans up a detached hit sound even while gameplay time is paused.</summary>
    public sealed class ImpactAudioLifetime : MonoBehaviour
    {
        public double ExpiresAt { get; set; }
        private void Update()
        {
            if (Time.unscaledTimeAsDouble >= ExpiresAt) Destroy(gameObject);
        }
    }
}
