using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;



public enum Estados
{
    Parado,
    BuscandoAsiento,
    Sentado,
    Bajando
}


public class Pasajero : MonoBehaviour
{
    public Estados _Estados;
    public Transform[] puntos;
    public List<Asiento> _Asientos;
    
 
    public void Start()
    {
        _Estados = Estados.Parado;
    }

    public void BuscoAsiento()
    {
        _Estados = Estados.BuscandoAsiento;
        foreach (Asiento asiento in _Asientos)
        {
            if (asiento.disponible)
            {
                return;
            }
        }
    }
    public void Camino_Al_Asiento()
    {
        for (int i = 0; i < puntos.Length; i++)
        {

        }
    }
    //public void EsperoParada()
    //{

    //}
}
