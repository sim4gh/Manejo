using UnityEngine;
using Gley.TrafficSystem;

public class TrafficZoneController : MonoBehaviour
{
    [Header("Configuracion de Carros activos")]
    public int _CuadrosActivos = 4;
    public int _maxVehiculos = 80;




    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerComponent>())
        {
            Debug.Log("si entra");
            API.SetActiveSquares(_CuadrosActivos);
            API.SetTrafficDensity(_maxVehiculos);
            Debug.Log($"[Traffic Zone] cuadros = {_CuadrosActivos} vehiculos Ahora ={_maxVehiculos}");

        }
    }
}
