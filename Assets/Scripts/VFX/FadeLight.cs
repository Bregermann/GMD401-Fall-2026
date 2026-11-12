using UnityEngine;

// A point light that flashes and fades out, then removes itself. Made by PlaceholderEffects.LightFlash.
// flicker > 0 keeps it alive and flickering instead (for burning auras), until the object is destroyed.
public class FadeLight : MonoBehaviour
{
    public float duration = 0.3f;
    public float flicker;

    private Light pointLight;
    private float startIntensity;
    private float age;

    void Start()
    {
        pointLight = GetComponent<Light>();
        startIntensity = pointLight.intensity;
    }

    void Update()
    {
        age += Time.deltaTime;

        if (flicker > 0f)
        {
            pointLight.intensity = startIntensity * (1f - flicker + flicker * Mathf.PerlinNoise(age * 8f, 0f));
            return;
        }

        float t = age / duration;
        pointLight.intensity = startIntensity * (1f - t) * (1f - t); // Quick fall-off
        if (t >= 1f) Destroy(gameObject);
    }
}
