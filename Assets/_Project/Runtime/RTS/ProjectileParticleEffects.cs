using UnityEngine;

namespace MRStrategyRTS.RTS
{
    [DisallowMultipleComponent]
    public sealed class ProjectileParticleEffects : MonoBehaviour
    {
        public GameObject flightEffectPrefab;
        public GameObject impactEffectPrefab;
        [Tooltip("原始子弹预制体的模型缩放，用于让特效随地图缩放，同时保持正确的米制尺寸。")]
        [Min(0.0001f)] public float referenceProjectileScale = 0.006f;
        [Min(0.01f)] public float effectScale = 1f;
        [Min(0.1f)] public float trailCleanupDelay = 0.5f;
        [Min(0.1f)] public float impactCleanupDelay = 0.65f;
        private GameObject flight;
        private bool completed;

        public void Begin()
        {
            if (flight != null || completed || flightEffectPrefab == null)
                return;
            flight = Instantiate(flightEffectPrefab, transform, false);
            flight.transform.localPosition = Vector3.zero;
            flight.transform.localRotation = Quaternion.identity;
            flight.transform.localScale = Vector3.one * (effectScale / referenceProjectileScale);
            foreach (var system in flight.GetComponentsInChildren<ParticleSystem>(true))
                system.Play(false);
        }

        public void Complete(bool hitTarget)
        {
            if (completed)
                return;
            completed = true;
            if (flight != null)
            {
                flight.transform.SetParent(null, true);
                foreach (var system in flight.GetComponentsInChildren<ParticleSystem>(true))
                    system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                Destroy(flight, CleanupDelay(flight, trailCleanupDelay, false));
                flight = null;
            }
            if (!hitTarget || impactEffectPrefab == null)
                return;
            var impact = Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            var scale = transform.lossyScale;
            var worldScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            impact.transform.localScale = Vector3.one * (worldScale * effectScale / referenceProjectileScale);
            foreach (var system in impact.GetComponentsInChildren<ParticleSystem>(true))
                system.Play(false);
            Destroy(impact, CleanupDelay(impact, impactCleanupDelay, true));
        }

        private static float CleanupDelay(GameObject effect, float minimum, bool includeEmission)
        {
            var delay = minimum;
            foreach (var system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                var lifetime = CurveMaximum(main.startLifetime);
                if (includeEmission)
                    lifetime += CurveMaximum(main.startDelay) + main.duration;
                delay = Mathf.Max(delay, lifetime / Mathf.Max(.01f, main.simulationSpeed) + .05f);
            }
            return delay;
        }

        private static float CurveMaximum(ParticleSystem.MinMaxCurve curve)
        {
            if (curve.mode == ParticleSystemCurveMode.Constant || curve.mode == ParticleSystemCurveMode.TwoConstants)
                return Mathf.Max(curve.constantMin, curve.constantMax);
            var maximum = 0f;
            for (var i = 0; i <= 32; i++)
                maximum = Mathf.Max(maximum, curve.Evaluate(i / 32f, 0f), curve.Evaluate(i / 32f, 1f));
            return maximum;
        }

        private void OnDestroy()
        {
            // External resets and scene teardown must not leave a live emitter behind.
            if (flight != null)
                Destroy(flight);
        }
    }
}
