using UnityEditor.Experimental.GraphView;
using UnityEngine;


public class Asiento : MonoBehaviour
{

    public string tipoAsiento;
    public int lugaresDisponibles = 2;
    private void OnTriggerEnter(Collider other)
    {
        if (lugaresDisponibles <= 0) return;

        if (other.GetComponentInParent<Pasajero>() != null)
        {
            lugaresDisponibles--;
            GetComponent<Collider>().enabled = false;

            Invoke(nameof(ActivarCollider), 1f);
        }
    }

    private void ActivarCollider()
    {
        GetComponent<Collider>().enabled = true;
    }



}
