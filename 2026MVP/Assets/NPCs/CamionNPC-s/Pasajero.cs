using System.Collections.Generic;
using UnityEngine;

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
    public GameObject _BusTransform;

    [Header("Animacion y variables de personaje")]
    public Animator animaciones;
    public int indiceActual = 0;
    public float velocidad = 2;
    public bool _Sentado = false;

    void Start()
    {
        animaciones.Play("Idle");
    }



    public void BuscoAsiento()
    {
        if (_Sentado) return;

        if (!_ViAsiento)
        {
            animaciones.Play("Walk");
            Transform destino = puntos[indiceActual];

            float distancia = Vector3.Distance(transform.position, destino.position);
            
         
            
                if (distancia > 0.2f)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position,
                        destino.position,
                        velocidad * Time.fixedDeltaTime
                    );

                    if (transform.position != _Asientos[Num_Asiento].transform.position)
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
                    transform.rotation = Quaternion.Euler(0, 270, 0);
                    break;

                case "MirandoDeFrente":
                    transform.rotation = Quaternion.Euler(0, 90, 0);
                    break;
            }

            Transform asientoDestino = _Asientos[Num_Asiento].transform;

            transform.position = Vector3.MoveTowards(
                transform.position,
                asientoDestino.position,
                velocidad * Time.fixedDeltaTime
            );

            if (transform.position == asientoDestino.position)
            {
                Sentado();
            }
        }
    }

    public void Sentado()
    {
        animaciones.Play("Sentado");
        transform.SetParent(_BusTransform.transform);
        _Sentado = true; 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Asientos"))
        {
            AsientoOrientacion = other.GetComponent<Asiento>().tipoAsiento;
            _ViAsiento = true;
            GetComponent<Collider>().enabled = false;
        }
    }
}