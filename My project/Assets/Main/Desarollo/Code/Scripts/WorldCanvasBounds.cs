using Oculus.Interaction.Surfaces;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform), typeof(BoundsClipper))]
public sealed class WorldCanvasBounds : MonoBehaviour
{
    private void Awake() => Resize();
    private void OnEnable() => Resize();
    private void OnRectTransformDimensionsChange() => Resize();
    private void Resize()
    {
        var clipper = GetComponent<BoundsClipper>();
        if (!clipper) return;
        var rect = ((RectTransform)transform).rect;
        // Keep a 2 cm tolerance for float rounding at large world coordinates.
        float depth = 0.02f / Mathf.Max(Mathf.Abs(transform.lossyScale.z), 0.000001f);
        clipper.Size = new Vector3(rect.width, rect.height, depth);
    }
}
