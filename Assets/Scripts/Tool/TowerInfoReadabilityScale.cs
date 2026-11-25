using MRStrategyRTS.Input;
using UnityEngine;
using UnityEngine.XR;

/// <summary>Scales only tower cards for viewing distance; the layout stays authored in the prefab.</summary>
[DefaultExecutionOrder(150), DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
public sealed class TowerInfoReadabilityScale : MonoBehaviour
{
    [SerializeField, Range(0f, 45f)] private float minimumAngularWidth = 28f;
    [SerializeField, Min(0f)] private float minimumGlyphPixels = 24f;
    [SerializeField, Min(1f)] private float minimumGlyphHeightUnits = 48f;
    [SerializeField, Range(.5f, .95f)] private float viewportWidthFraction = .85f;
    [SerializeField, Range(.5f, .95f)] private float viewportHeightFraction = .8f;
    private RectTransform rect;
    private Vector3 authoredScale;
    private TerrainTwoHandPinchManipulator terrain;
    private Camera viewer;

    private void Awake()
    {
        rect = (RectTransform)transform;
        authoredScale = transform.localScale;
        terrain = GetComponentInParent<TerrainTwoHandPinchManipulator>();
    }

    private void LateUpdate()
    {
        if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
        if (viewer == null || rect == null) return;
        var multiplier = terrain != null ? Mathf.Max(.001f, terrain.CurrentScaleMultiplier) : 1f;
        var baseline = authoredScale / multiplier;
        var right = transform.parent != null
            ? transform.parent.TransformVector(Vector3.right * baseline.x)
            : Vector3.right * baseline.x;
        var up = transform.parent != null
            ? transform.parent.TransformVector(Vector3.up * baseline.y)
            : Vector3.up * baseline.y;
        var width = rect.rect.width * right.magnitude;
        var height = rect.rect.height * up.magnitude;
        if (width <= .000001f || height <= .000001f) return;
        var distance = Mathf.Max(.05f, Vector3.Dot(transform.position - viewer.transform.position, viewer.transform.forward));
        var aspect = viewer.aspect;
        float visibleHeight;
        if (viewer.orthographic) visibleHeight = viewer.orthographicSize * 2f;
        else if (viewer.stereoEnabled)
        {
            var projection = viewer.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            visibleHeight = 2f * distance / Mathf.Max(.001f, Mathf.Abs(projection.m11));
            aspect = Mathf.Abs(projection.m11 / projection.m00);
        }
        else visibleHeight = 2f * distance * Mathf.Tan(viewer.fieldOfView * Mathf.Deg2Rad / 2f);
        var pixelHeight = viewer.stereoEnabled && XRSettings.enabled && XRSettings.eyeTextureHeight > 0
            ? XRSettings.eyeTextureHeight : viewer.pixelHeight;
        var projectedGlyphPixels = minimumGlyphHeightUnits * up.magnitude
            / Mathf.Max(.000001f, visibleHeight) * Mathf.Max(1, pixelHeight);
        var angularWidth = viewer.orthographic ? 0f
            : 2f * distance * Mathf.Tan(minimumAngularWidth * Mathf.Deg2Rad / 2f);
        var desired = Mathf.Max(1f, Mathf.Max(angularWidth / width,
            minimumGlyphPixels / Mathf.Max(.001f, projectedGlyphPixels)));
        var fit = Mathf.Min(visibleHeight * aspect * viewportWidthFraction / width,
            visibleHeight * viewportHeightFraction / height);
        // On a small viewport, fit the complete panel instead of clipping its outer rows.
        transform.localScale = baseline * Mathf.Max(.01f, Mathf.Min(desired, fit));
    }
}
