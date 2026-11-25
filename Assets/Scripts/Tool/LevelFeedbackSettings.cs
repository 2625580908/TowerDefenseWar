using UnityEngine;

[CreateAssetMenu(menuName = "MRStrategyRTS/Level Feedback Settings")]
public sealed class LevelFeedbackSettings : ScriptableObject
{
    [Header("结算特效")]
    public GameObject victoryParticles;
    public GameObject defeatParticles;
    [Min(0.1f)] public float particleDuration = 4.5f;
    [Min(0.2f)] public float effectDistance = 0.8f;
    public float effectHeightOffset = 0.12f;
    [Header("独立音效")]
    public AudioClip victorySound;
    public AudioClip defeatSound;
    public AudioClip damageSound;
    [Range(0, 1)] public float resultVolume = 0.65f;
    [Range(0, 1)] public float damageVolume = 0.45f;
    [Header("摄像机受击闪红")]
    public Material damageOverlayMaterial;
    [Range(0.1f, 1.5f)] public float damageFlashDuration = 0.45f;
    [Range(0, 0.65f)] public float damageFlashOpacity = 0.28f;
}
