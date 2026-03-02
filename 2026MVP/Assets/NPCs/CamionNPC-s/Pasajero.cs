using System.Collections.Generic;
using NUnit.Framework;
using Unity.VisualScripting;
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
    public List<Transform> puntos;
    public List<Asiento> _Asientos;
    public Rigidbody rb;
    public float velocidad = 2;
    public float _LaDistanciaAl_Asiento = 0;
    public GameObject PuntosRecorrido;
    public Animator animaciones;
    public Asiento Asiento;

    public void Start()
    {
        _Estados = Estados.Parado;
        rb.MovePosition(Vector3.MoveTowards(rb.position, puntos[0].position, velocidad * Time.fixedDeltaTime));
        animaciones.Play("Female_Armature|Female_Armature|Female_Armature|TSP_Male_Pose_Sitting_01|Female");

    }



    void FixedUpdate()
    {
        animaciones.Play("Walk");
        BuscoAsiento();
       
    }




    public void BuscoAsiento()
    {
        
        _Estados = Estados.BuscandoAsiento;
        for (int i = 0; i < puntos.Count; i++)
        {
            rb.MovePosition(Vector3.MoveTowards(rb.position, puntos[i].position, velocidad * Time.fixedDeltaTime));
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Asientos"))
        {
            switch (Asiento._Orientation)
            {
                case AsientoOrientacion.Derecho:
                    rb.rotation = Quaternion.Euler(0,90,0); 
                    break;
                case AsientoOrientacion.Izquierdo:
                    break;
                case AsientoOrientacion.Atras:
                    break;
                default:
                    break;
            }
            if(Asiento._Orientation==AsientoOrientacion.Izquierdo)
              {

              }
            rb.MovePosition(Vector3.MoveTowards(rb.position, _Asientos[0].transform.position, velocidad * Time.fixedDeltaTime));

        }
    }

   
    //public void EsperoParada()
    //{

    //}
}
