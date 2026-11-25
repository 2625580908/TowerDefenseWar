using MRStrategyRTS.Config;
using UnityEngine;

namespace MRStrategyRTS.RTS
{
    /// <summary>A shared impact audio gate keeps simultaneous hits from stacking voices.</summary>
    public static class TowerImpactAudio
    {
        private static double nextImpactAt = double.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => nextImpactAt = double.NegativeInfinity;

        public static void Play(TowerDefinition definition, Vector3 position)
        {
            if (definition == null || definition.impactSound == null || definition.impactSoundVolume <= 0) return;
            var now = Time.unscaledTimeAsDouble;
            if (now < nextImpactAt) return;
            var clip = definition.impactSound;
            nextImpactAt = now + System.Math.Max(.08,clip.length);
            var go = new GameObject("Tower Impact Audio");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = .08f;
            source.maxDistance = 3f;
            source.clip = clip;
            source.volume = definition.impactSoundVolume;
            source.Play();
            // Audio keeps playing when scaled game time is paused.
            go.AddComponent<ImpactAudioLifetime>().ExpiresAt = now + clip.length + .08;
        }
    }
}
