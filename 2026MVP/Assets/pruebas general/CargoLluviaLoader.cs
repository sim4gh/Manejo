using UnityEngine;

/// <summary>
/// DEPRECATED — reemplazado por <see cref="WeatherManager"/>.
///
/// Este script fue el sistema original de "lluvia random" controlado por
/// <c>PlayerPrefs["Cargolluvia"]</c>. Nunca funcionó porque <c>objetoLluvia</c>
/// estaba sin asignar (<c>fileID: 0</c>) en las 6 escenas custom; el SetActive
/// fallaba con NullReferenceException o no hacía nada.
///
/// El nuevo sistema (<see cref="WeatherManager"/>, singleton bootstrapped) lee
/// <c>PlayerPrefs["Clima"]</c> y orquesta sol/lluvia/granizo sin necesidad de
/// wiring por escena.
///
/// TODO (próximo iter): borrar este script y eliminar los componentes
/// <c>CargoLluviaLoader</c> huérfanos en las 6 escenas (Sedan, Camioneta,
/// Motocicleta, BusPasajeros, CamionDCarga, Ambulancia). Requiere abrir cada
/// escena en Unity Editor — no hacerlo desde fuera tocando YAML directamente.
/// </summary>
[System.Obsolete("Reemplazado por WeatherManager. TODO: eliminar este script y los componentes huérfanos en las 6 escenas en próximo iter.")]
public class CargoLluviaLoader : MonoBehaviour
{
    public GameObject objetoLluvia;

    void Start()
    {
        // No-op transicional. El control del clima ahora vive en WeatherManager.
    }
}
