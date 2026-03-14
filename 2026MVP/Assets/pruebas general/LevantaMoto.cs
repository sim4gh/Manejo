using UnityEngine;

public class LevantaMoto : MonoBehaviour
{
    [Header("Configuración")]
    public float rotationThreshold = 80f;  // Límite de rotación en Z
    public float delayBeforeReset = 5f;    // Segundos antes de resetear

    private bool isCounting = false;
    private float timer = 0f;

    void Update()
    {
        float rotZ = NormalizeAngle(transform.eulerAngles.z);

        if (rotZ >= rotationThreshold || rotZ <= -rotationThreshold)
        {
            timer += Time.deltaTime;

            if (timer >= delayBeforeReset)
            {
                // Reset súbito a 0 en Z
                transform.eulerAngles = new Vector3(
                    transform.eulerAngles.x,
                    transform.eulerAngles.y,
                    0f
                );
                timer = 0f;
            }
        }
        else
        {
            // Si regresa al rango normal, reiniciamos el timer
            timer = 0f;
        }
    }

    // Convierte ángulos de [0, 360] a [-180, 180]
    float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
