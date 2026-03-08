using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
public class Asiento : MonoBehaviour
{
    public string tipoAsiento;
    public int lugaresDisponibles = 2;
    public List<Asiento> asientosDisponibles;
    public bool _UnoMenos = false;
    private void OnTriggerEnter(Collider other)
    {
        int TeDoyAsiento = 0;
        if (lugaresDisponibles <= 0) return;
        if (other.GetComponentInParent<Pasajero>())
        {
            if (lugaresDisponibles == 2)
            {
                other.GetComponentInParent<Pasajero>()._Asientos[0] = asientosDisponibles[0];
                other.GetComponentInParent<Pasajero>().Num_Asiento = TeDoyAsiento;
            }
            else
            {
                other.GetComponentInParent<Pasajero>()._Asientos[1] = asientosDisponibles[1];
                other.GetComponentInParent<Pasajero>().Num_Asiento = TeDoyAsiento + 1;
            }
            if (!_UnoMenos)
            {
                _UnoMenos = true;
                lugaresDisponibles--;
                Invoke(nameof(Activarbooleano), 2f);
            }
        }
    }
    private void Activarbooleano()
    {
        _UnoMenos = false;
    }
}