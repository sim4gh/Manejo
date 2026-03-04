using UnityEngine;
using Gley.TrafficSystem;

public class DetenerAnimacion : MonoBehaviour
{
    public Animator bikeAnimator;
    public TwoWheelComponent twoWheel;

    private float stopThreshold = 0.5f;

    void Update()
    {
        float speed = twoWheel.GetCurrentSpeedMS();

        if (speed < stopThreshold)
        {
            bikeAnimator.speed = 0f;   // Detiene animación
        }
        else
        {
            bikeAnimator.speed = 1f;   // Activa animación
        }
    }
}