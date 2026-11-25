using UnityEngine;

/// <summary>Aligns the rendered base of a display to the slot surface, including rotated/scaled shops.</summary>
public static class ShopDisplaySurfacePlacement
{
    public static bool PlaceOnSlot(Transform display, Transform slot, float clearance,
        Transform layoutSpace = null, Vector3 referenceOffset = default)
    {
        // Tile rotation describes the imported mesh, not the shop's upward direction.
        if (layoutSpace == null) layoutSpace = slot.parent != null ? slot.parent : slot;
        referenceOffset.y = 0;
        display.position = slot.position + layoutSpace.TransformVector(referenceOffset);
        if (!TryGetHeightRange(slot, layoutSpace, out _, out var surfaceTop)
            || !TryGetHeightRange(display, layoutSpace, out var modelBottom, out _))
            return false;

        display.position += layoutSpace.TransformVector(
            Vector3.up * (surfaceTop - modelBottom + Mathf.Max(0, clearance)));
        return true;
    }

    private static bool TryGetHeightRange(Transform root, Transform space, out float bottom, out float top)
    {
        bottom = float.PositiveInfinity;
        top = float.NegativeInfinity;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null || renderer == null || !renderer.enabled
                || !IsActiveBelowRoot(filter.transform, root)) continue;
            var bounds = filter.sharedMesh.bounds;
            for (var i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var height = space.InverseTransformPoint(filter.transform.TransformPoint(corner)).y;
                bottom = Mathf.Min(bottom, height);
                top = Mathf.Max(top, height);
            }
        }
        return !float.IsInfinity(bottom);
    }

    private static bool IsActiveBelowRoot(Transform child, Transform root)
    {
        for (var current = child; current != root; current = current.parent)
            if (!current.gameObject.activeSelf) return false;
        return true;
    }
}
