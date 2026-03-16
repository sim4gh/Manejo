using UnityEngine;

public class LevantaMoto : MonoBehaviour
{
    [Header("Configuración")]
    public float rotationThreshold = 80f;  
    public float delayBeforeReset = 5f;    

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
            
            timer = 0f;
        }
    }

    
    float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
