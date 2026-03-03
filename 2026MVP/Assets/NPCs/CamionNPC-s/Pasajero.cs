using NUnit.Framework;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;






public class Pasajero : MonoBehaviour
{
    [Header("Listas de puntos")]
    public List<Transform> puntos;
    public List<Asiento> _Asientos;

    [Header("Variables de Asiento y referencias")]
    public string AsientoOrientacion;
    public GameObject PuntosRecorrido;
    public Asiento Asiento;
    public bool _ViAsiento = false;
    public int Num_Asiento = 0;

    [Header("Animacion y variables de personaje")]
    public Animator animaciones;
    public int indiceActual = 0;
    public Rigidbody rb;
    public float velocidad = 2;

    public void Start()
    {
        rb.MovePosition(Vector3.MoveTowards(rb.position, puntos[0].position, velocidad * Time.fixedDeltaTime));
        animaciones.Play("Walk");


    }



    void FixedUpdate()
    {
        BuscoAsiento();
       
    }



    public void BuscoAsiento()
    {

        if (!_ViAsiento)
        {

            Transform destino = puntos[indiceActual];

            float distancia = Vector3.Distance(rb.position, destino.position);

            if (distancia > 0.2f)
            {
                rb.MovePosition(Vector3.MoveTowards(rb.position, destino.position, velocidad * Time.fixedDeltaTime));

                if (rb.position != _Asientos[Num_Asiento].transform.position)
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
            switch (AsientoOrientacion)
            {
                case "MirandoAtras":
                    rb.rotation = Quaternion.Euler(0, 270, 0);
                    break;

                case "MirandoDeFrente":
                    rb.rotation = Quaternion.Euler(0, 90, 0);

                    break;

                default:
                    break;
            }

            Transform asientoDestino = _Asientos[Num_Asiento].transform;
          
            rb.MovePosition(Vector3.MoveTowards(rb.position, asientoDestino.position, velocidad * Time.fixedDeltaTime));
         
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
            AsientoOrientacion = Asiento.tipoAsiento;
            _ViAsiento = true;
        }
    }

   
 
}
