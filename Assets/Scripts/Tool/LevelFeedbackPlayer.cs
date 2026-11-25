using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Shared by normal and designed levels; does not modify camera clear/passthrough settings.</summary>
[DisallowMultipleComponent]
public sealed class LevelFeedbackPlayer : MonoBehaviour
{
    private LevelFeedbackSettings settings;
    private AudioSource resultAudio;
    private AudioSource damageAudio;
    private Camera viewCamera;
    private GameObject overlay;
    private Mesh overlayMesh;
    private Material overlayMaterial;
    private MeshRenderer overlayRenderer;
    private GameObject resultEffect;
    private float flashRemaining;
    private float effectRemaining;
    private bool resultPlayed;
    private static readonly int Opacity = Shader.PropertyToID("_Opacity");

    private void Awake()
    {
        settings = Resources.Load<LevelFeedbackSettings>("LevelFeedbackSettings");
        resultAudio = CreateAudio("Level Result Audio");
        damageAudio = CreateAudio("Base Damage Audio");
        if (settings == null)
            Debug.LogWarning("Missing Resources/LevelFeedbackSettings; level feedback is disabled.", this);
    }

    private AudioSource CreateAudio(string objectName)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.dopplerLevel = 0;
        return source;
    }

    private void OnEnable() => Camera.onPreCull += BeforeCameraRender;

    private void OnDisable()
    {
        Camera.onPreCull -= BeforeCameraRender;
        Clear();
    }

    public void PlayDamage()
    {
        if (settings == null || !isActiveAndEnabled) return;
        if (settings.damageSound != null)
            damageAudio.PlayOneShot(settings.damageSound, settings.damageVolume);
        flashRemaining = settings.damageFlashDuration;
        EnsureOverlay();
        if (overlayMaterial != null)
            overlayMaterial.SetFloat(Opacity, settings.damageFlashOpacity);
    }

    public void PlayResult(bool victory)
    {
        if (resultPlayed || settings == null || !isActiveAndEnabled) return;
        resultPlayed = true;
        var clip = victory ? settings.victorySound : settings.defeatSound;
        if (clip != null) resultAudio.PlayOneShot(clip, settings.resultVolume);
        var prefab = victory ? settings.victoryParticles : settings.defeatParticles;
        var camera = Camera.main;
        if (prefab == null || camera == null) return;
        var position = camera.transform.position + camera.transform.forward * settings.effectDistance
                       + Vector3.up * settings.effectHeightOffset;
        resultEffect = Instantiate(prefab, position, Quaternion.identity);
        effectRemaining = settings.particleDuration;
        foreach (var particles in resultEffect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.useUnscaledTime = true;
            particles.Play(false);
        }
    }

    public void Clear()
    {
        resultPlayed = false;
        flashRemaining = 0;
        effectRemaining = 0;
        if (resultAudio != null) resultAudio.Stop();
        if (damageAudio != null) damageAudio.Stop();
        if (overlayRenderer != null) overlayRenderer.enabled = false;
        if (overlayMaterial != null) overlayMaterial.SetFloat(Opacity, 0);
        if (resultEffect != null)
        {
            resultEffect.SetActive(false);
            Destroy(resultEffect);
            resultEffect = null;
        }
    }

    private void Update()
    {
        if (resultEffect != null)
        {
            effectRemaining -= Time.unscaledDeltaTime;
            if (effectRemaining <= 0)
            {
                Destroy(resultEffect);
                resultEffect = null;
            }
        }
        if (flashRemaining <= 0) return;
        EnsureOverlay();
        flashRemaining = Mathf.Max(0, flashRemaining - Time.unscaledDeltaTime);
        if (overlayMaterial != null)
        {
            var fraction = flashRemaining / Mathf.Max(0.01f, settings.damageFlashDuration);
            overlayMaterial.SetFloat(Opacity, settings.damageFlashOpacity * fraction * fraction);
        }
        if (flashRemaining == 0 && overlayRenderer != null) overlayRenderer.enabled = false;
    }

    private void BeforeCameraRender(Camera camera)
    {
        if (overlayRenderer != null)
            overlayRenderer.enabled = camera == viewCamera && flashRemaining > 0;
    }

    private void EnsureOverlay()
    {
        var camera = Camera.main;
        if (camera == null || settings.damageOverlayMaterial == null) return;
        if (overlay == null)
        {
            if (overlayMesh != null) Destroy(overlayMesh);
            if (overlayMaterial != null) Destroy(overlayMaterial);
            overlay = new GameObject("Damage Red Vignette");
            overlayMesh = new Mesh { name = "Damage Screen Quad" };
            overlayMesh.vertices = new[]
            {
                new Vector3(-1, -1, 0), new Vector3(-1, 1, 0),
                new Vector3(1, 1, 0), new Vector3(1, -1, 0)
            };
            overlayMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            overlayMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
            overlay.AddComponent<MeshFilter>().sharedMesh = overlayMesh;
            overlayRenderer = overlay.AddComponent<MeshRenderer>();
            overlayMaterial = new Material(settings.damageOverlayMaterial);
            overlayRenderer.sharedMaterial = overlayMaterial;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlayRenderer.enabled = false;
        }
        viewCamera = camera;
        overlay.transform.SetParent(camera.transform, false);
        overlay.transform.localPosition = Vector3.forward * (camera.nearClipPlane + 0.1f);
        // Use a layer the gameplay camera already renders; never change its culling mask.
        for (var layer = 0; layer < 32; layer++)
        {
            if ((camera.cullingMask & (1 << layer)) == 0) continue;
            overlay.layer = layer;
            break;
        }
    }

    private void OnDestroy()
    {
        Clear();
        if (overlay != null) Destroy(overlay);
        if (overlayMesh != null) Destroy(overlayMesh);
        if (overlayMaterial != null) Destroy(overlayMaterial);
        if (resultAudio != null) Destroy(resultAudio.gameObject);
        if (damageAudio != null) Destroy(damageAudio.gameObject);
    }
}
