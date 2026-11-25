using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.XR;

public class PicoVideoSeeThroughManager : MonoBehaviour
{
    [Header("Eye Buffer")]
    [Tooltip("XR eye-buffer resolution scale. Higher values improve clarity but increase GPU cost.")]
    [Range(0.7f, 2f)]
    public float eyeBufferScale = 1f;

    private void Awake()
    {
        PXR_Manager.EnableVideoSeeThrough = true;
        ApplyEyeBufferScale();
    }

    private void Start()
    {
        ApplyEyeBufferScale();
    }

    public void SetEyeBufferScale(float value)
    {
        eyeBufferScale = Mathf.Clamp(value, 0.7f, 1.3f);
        ApplyEyeBufferScale();
    }

    private void ApplyEyeBufferScale()
    {
        XRSettings.eyeTextureResolutionScale = eyeBufferScale;
    }

    private void OnValidate()
    {
        eyeBufferScale = Mathf.Clamp(eyeBufferScale, 0.7f, 1.3f);
        if (Application.isPlaying)
        {
            ApplyEyeBufferScale();
        }
    }
}
