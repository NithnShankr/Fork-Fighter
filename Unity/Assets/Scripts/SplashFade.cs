using UnityEngine;

public class SplashFade : MonoBehaviour
{
    [Tooltip("Seconds it takes for the splash to fully fade out.")]
    public float fadeDuration = 1.5f;

    [Tooltip("If true, the script will automatically clone the material to avoid modifying the shared one.")]
    public bool cloneMaterialIfNeeded = true;

    private Material _material;
    private Color _originalColor;
    private float _timer = 0f;
    private bool _fading = false;

    private void Awake()
    {
        // Get renderer
        Renderer rend = GetComponent<Renderer>();
        if (rend == null)
        {
            Debug.LogWarning("SplashFade: No Renderer found on splash object.");
            enabled = false;
            return;
        }

        // Clone material only if required
        if (cloneMaterialIfNeeded)
        {
            _material = Instantiate(rend.material);
            rend.material = _material;
        }
        else
        {
            _material = rend.material; // modify existing
        }

        _originalColor = _material.color;
    }

    private void OnEnable()
    {
        // Reset fade state
        _timer = 0f;
        _fading = true;

        if (_material != null)
        {
            Color c = _material.color;
            c.a = _originalColor.a;
            _material.color = c;
        }
    }

    private void Update()
    {
        if (!_fading || _material == null) return;

        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / fadeDuration);

        Color c = _originalColor;
        c.a = Mathf.Lerp(_originalColor.a, 0f, t);
        _material.color = c;

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
