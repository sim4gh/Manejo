using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class GPSLineDrawer : MonoBehaviour
{
    [Header("Configuración de la línea")]
    public float lineWidth = 4f;
    public Color lineColor = new Color(0.2f, 0.7f, 1f, 0.9f); // Azul GPS

    private RectTransform _rect;
    private Image _image;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _image = GetComponent<Image>();

        if (_image != null)
            _image.color = lineColor;
    }

    /// <summary>
    /// Llama esto desde GPSController.LateUpdate() cada frame.
    /// from/to son posiciones anchoredPosition dentro del GPS Canvas.
    /// </summary>
    public void UpdateLine(Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Tamaño: largo = distancia entre iconos, alto = grosor de la línea
        _rect.sizeDelta = new Vector2(distance, lineWidth);

        // Pivot en el extremo izquierdo para que arranque desde 'from'
        _rect.pivot = new Vector2(0f, 0.5f);

        // Posicionar en el punto de origen (carro, generalmente centro)
        _rect.anchoredPosition = from;

        // Rotar hacia el destino
        _rect.localEulerAngles = new Vector3(0f, 0f, angle);
    }
}
