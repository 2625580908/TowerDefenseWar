using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shared 2D feedback for scene-authored and runtime UI buttons.</summary>
[DisallowMultipleComponent]
public sealed class UIClickAudioPlayer : MonoBehaviour
{
    private static UIClickAudioPlayer instance;
    private UIClickAudioSettings settings;
    private AudioSource source;
    private double lastClickTime = double.NegativeInfinity;
    private float nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;
        instance = FindFirstObjectByType<UIClickAudioPlayer>();
        if (instance == null)
            new GameObject("UI Click Audio").AddComponent<UIClickAudioPlayer>();
        else
            instance.ConfigureAudio();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        ConfigureAudio();
    }

    private void ConfigureAudio()
    {
        settings = Resources.Load<UIClickAudioSettings>("UIAudioSettings");
        source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.priority = 32;
        source.ignoreListenerPause = true;
        source.dopplerLevel = 0f;
        BindSceneButtons();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindSceneButtons();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextScanTime)
            return;
        nextScanTime = Time.unscaledTime + 0.5f;
        // Also repairs hooks after other systems replace their ButtonClickedEvent.
        BindSceneButtons();
    }

    private static void BindSceneButtons()
    {
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Register(button);
    }

    public static void Register(Button button)
    {
        if (button == null || !Application.isPlaying)
            return;
        button.onClick.RemoveListener(PlayClick);
        button.onClick.AddListener(PlayClick);
    }

    public static void PlayClick()
    {
        if (!Application.isPlaying)
            return;
        if (instance == null)
            Bootstrap();
        if (instance == null || instance.settings == null || instance.settings.clickSound == null)
            return;
        var now = Time.realtimeSinceStartupAsDouble;
        if (now - instance.lastClickTime < instance.settings.minimumClickInterval)
            return;
        instance.lastClickTime = now;
        instance.source.clip = instance.settings.clickSound;
        instance.source.volume = instance.settings.volume;
        // A single voice keeps rapid brush clicks from accumulating volume.
        instance.source.Play();
    }
}
