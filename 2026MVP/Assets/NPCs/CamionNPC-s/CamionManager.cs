using System.Collections.Generic;
using UnityEngine;
public class CamionManager : MonoBehaviour
{
    public static CamionManager instancia;
    public List<Pasajero> pasajerosABordo = new List<Pasajero>();

    void Awake()
    {
        instancia = this;
    }
}