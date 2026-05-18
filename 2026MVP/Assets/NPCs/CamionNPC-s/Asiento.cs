using System.Collections.Generic;

using UnityEngine;
public class Asiento : MonoBehaviour
{
    public string tipoAsiento;
    public List<Pasajero> pasajerosSentados = new List<Pasajero> { null, null };
    public int lugaresDisponibles = 2;
    public List<Asiento> asientosDisponibles;
    public bool _UnoMenos = false;

    private void OnTriggerEnter(Collider other)
    {
        if (lugaresDisponibles <= 0) return;
        if (_UnoMenos) return;

        Pasajero pasajero = other.GetComponentInParent<Pasajero>();
        if (pasajero == null) return;

        
        int indice = -1;
        for (int i = 0; i < pasajerosSentados.Count; i++)
        {
            if (pasajerosSentados[i] == null)
            {
                indice = i;
                break;
            }
        }
        if (indice == -1) return; 

        pasajero._Asientos[indice] = asientosDisponibles[indice];
        pasajero.Num_Asiento = indice;
        pasajero._MiAsientoDespachador = this; //aqui es donde el pasajero recuerda donde se sienta xd
        pasajerosSentados[indice] = pasajero;

        _UnoMenos = true;
        lugaresDisponibles--;
        Invoke(nameof(Activarbooleano), 2f);
    }

    private void Activarbooleano()
    {
        _UnoMenos = false;
    }
}