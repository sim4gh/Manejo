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
    public GameObject PuntosRecorrido;
    public Animator animaciones;
    public Asiento Asiento;
    public bool _ViAsiento = false;
    public int indiceActual = 0;

    public void Start()
    {
        _Estados = Estados.Parado;
        rb.MovePosition(Vector3.MoveTowards(rb.position, puntos[0].position, velocidad * Time.fixedDeltaTime));
        animaciones.Play("Walk");


    }



    void FixedUpdate()
    {
        BuscoAsiento();
       
    }



    public void BuscoAsiento()
    {
        _Estados = Estados.BuscandoAsiento;

        //switch (Asiento._Orientation)
        //{
        //    case AsientoOrientacion.Derecho:
        //        rb.rotation = Quaternion.Euler(0f, 0f, 0f);

        //        break;
        //    case AsientoOrientacion.Izquierdo:
        //        break;
        //    case AsientoOrientacion.Atras:
        //        break;
        //    default:
        //        break;
        //}

        if (!_ViAsiento)
        {

            Transform destino = puntos[indiceActual];

            float distancia = Vector3.Distance(rb.position, destino.position);

            if (distancia > 0.2f)
            {
                rb.MovePosition(Vector3.MoveTowards(rb.position, destino.position, velocidad * Time.fixedDeltaTime));

                if (rb.position != _Asientos[0].transform.position)
                {
                    transform.LookAt(destino);

                }
               
            }
            else
            {
                indiceActual++; 
            }
        }
        else
        {
           

            Transform asientoDestino = _Asientos[0].transform;

            rb.MovePosition(Vector3.MoveTowards(rb.position, asientoDestino.position, velocidad * Time.fixedDeltaTime));
            Quaternion angulo = Quaternion.Euler(0, 90, 0);
            rb.rotation = angulo;
            Invoke(nameof(Sentado), 1f);
        }
    }

    public void Sentado()
    {
        animaciones.Play("Female_Armature|Female_Armature|Female_Armature|TSP_Male_Pose_Sitting_01|Female");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Asientos"))
        {
            _ViAsiento = true;
            
           

        }
    }

   
    //public void EsperoParada()
    //{

    //}
}
