using Gley.TrafficSystem;
using UnityEngine;

public class ReduccionCarros : MonoBehaviour
{
    void Start()
    {
        Invoke(nameof(ReduzcoEscala), .2f);
    }

    public void ReduzcoEscala()
    {
        VehicleComponent[] vehicles = GetComponentsInChildren<VehicleComponent>();

        foreach (VehicleComponent vehicle in vehicles)
        {
            vehicle.transform.localScale = new Vector3(.9f, 1f, .9f);
        }
    }
}