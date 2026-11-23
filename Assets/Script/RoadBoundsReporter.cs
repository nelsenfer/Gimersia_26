
using UnityEngine;

public class RoadBoundsReporter : MonoBehaviour
{
    private void Start()
    {
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogError("Renderer aspal tidak ditemukan!");
            return;
        }

        Bounds bounds = renderers[0].bounds;

        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        Debug.Log("===== ROAD MEASUREMENT =====");
        Debug.Log("Center: " + bounds.center);
        Debug.Log("Size: " + bounds.size);
        Debug.Log("Minimum: " + bounds.min);
        Debug.Log("Maximum: " + bounds.max);
    }
}
