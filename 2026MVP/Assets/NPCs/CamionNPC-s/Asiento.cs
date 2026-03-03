using UnityEditor.Experimental.GraphView;
using UnityEngine;


public class Asiento : MonoBehaviour
{

    public string tipoAsiento;
    public int lugaresDisponibles = 2;
    public bool _UnoMenos = false;
    private void OnTriggerEnter(Collider other)
    {
        if (lugaresDisponibles <= 0) return;

        if (other.GetComponentInParent<Pasajero>())
        {
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
