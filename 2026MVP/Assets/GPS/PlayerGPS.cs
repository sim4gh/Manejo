using UnityEngine;
using UnityEngine.UI;

public class PlayerGPS : MonoBehaviour
{
    public float minScale = 0.8f;
    public float maxScale = 2f;
    public float speed = 2f;
    public float maxAlpha = 0.5f;

    private RectTransform rect;
    private Image image;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
    }

    void Update()
    {
        // Onda suave entre 0 y 1 (sube y baja como una respiración)
        float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;

        // Escala suave
        float scale = Mathf.Lerp(minScale, maxScale, t);
        rect.localScale = new Vector3(scale, scale, 1f);

        // Alpha suave (más opaco cuando es chico, transparente cuando crece)
        Color c = image.color;
        c.a = Mathf.Lerp(maxAlpha, 0f, t);
        image.color = c;
    }
}