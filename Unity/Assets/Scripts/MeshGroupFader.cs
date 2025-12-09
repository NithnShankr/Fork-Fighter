using UnityEngine;
using System.Collections;

public class URPAlphaReducer : MonoBehaviour
{
    public Renderer[] renderers;   // All meshes sharing the same URP Lit material
    public float duration = 2f;    // Time until alpha reaches 0

    private Material clonedMaterial;

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("No renderers assigned.");
            return;
        }

        // Clone once from the shared material
        clonedMaterial = Instantiate(renderers[0].sharedMaterial);

        // Apply to all renderers
        foreach (var r in renderers)
            r.material = clonedMaterial;
    }

    void Start()
    {
        StartCoroutine(ReduceAlpha());
    }

    private IEnumerator ReduceAlpha()
    {
        float t = 0f;

        Color start = clonedMaterial.GetColor("_BaseColor");
        Color target = start;
        target.a = 0f;

        while (t < duration)
        {
            float lerp = t / duration;
            clonedMaterial.SetColor("_BaseColor", Color.Lerp(start, target, lerp));
            t += Time.deltaTime;
            yield return null;
        }

        clonedMaterial.SetColor("_BaseColor", target);
    }
}
