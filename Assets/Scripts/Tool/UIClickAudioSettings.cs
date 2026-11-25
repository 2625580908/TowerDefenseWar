using UnityEngine;

[CreateAssetMenu(menuName = "MRStrategyRTS/UI Click Audio Settings")]
public sealed class UIClickAudioSettings : ScriptableObject
{
    public AudioClip clickSound;
    [Range(0f, 1f)] public float volume = 0.45f;
    [Tooltip("合并同一次操作触发的重复回调，避免叠音。")]
    [Range(0f, 0.1f)] public float minimumClickInterval = 0.04f;
}
