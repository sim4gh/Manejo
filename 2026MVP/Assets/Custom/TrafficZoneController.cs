using UnityEngine;
using Gley.TrafficSystem;
using Unity.VisualScripting;

public class TrafficZoneController : MonoBehaviour
{
    [Header("Configuracion de Carros activos")]
    public int _CuadrosActivos = 4;
    public int _maxVehiculos = 80;



    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerComponent>())
        {
            
            API.SetActiveSquares(_CuadrosActivos);
            API.SetTrafficDensity(_maxVehiculos);
            
        } 
       
    }



}
