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
    public float rotacionAl_AsientoDelantero = 0;
    public float rotacionAl_AsientoTrasero = 0;
    public float velocidad = 2;
    public bool _Sentado = false;
    [Header("Variables de bajada")]
    public bool _Bajando = false;
    public bool _BajarActivado = false;
    private List<Transform> puntosInversos = new List<Transform>();
    private int indiceBajada = 0;
    public bool _Activo = false;
    public string Mi_Parada;

    void Start()
    {
        animaciones.Play("Idle");
    }

    void Update()
    {
        if (!_Activo && !_BajarActivado) return;
        if (!_Sentado && !_Bajando && !_BajarActivado)
        {
            BuscoAsiento();
        }
        if (_BajarActivado || _Bajando)
        {
            BajarDelBus();
        }
    }

    public void BuscoAsiento()
    {
        if (_Sentado) return;
        if (!_ViAsiento)
        {
            if (indiceActual >= puntos.Count) return;
            animaciones.Play("Walk");
            Transform destino = puntos[indiceActual];
            float distancia = Vector3.Distance(transform.position, destino.position);
            if (distancia > 0.2f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    destino.position,
                    velocidad * Time.deltaTime
                );
                if (_Asientos.Count > Num_Asiento && _Asientos[Num_Asiento] != null)
                {
                    if (transform.position != _Asientos[Num_Asiento].transform.position)
                    {
                        transform.LookAt(destino);
                    }
                }
                else
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
                    transform.rotation = Quaternion.Euler(0, rotacionAl_AsientoTrasero, 0);
                    break;
                case "MirandoDeFrente":
                    transform.rotation = Quaternion.Euler(0, rotacionAl_AsientoDelantero, 0);
                    break;
            }
            Transform asientoDestino = _Asientos[Num_Asiento].transform;
            transform.position = Vector3.MoveTowards(
                transform.position,
                asientoDestino.position,
                velocidad * Time.deltaTime
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
        CamionManager.instancia.pasajerosABordo.Add(this);
    }

    public void BajarDelBus()
    {
        if (!_Sentado && !_Bajando) return; // solo sale si no está en ningún estado válido

        if (!_Bajando)
        {
            transform.SetParent(null);
            puntosInversos.Clear();
            puntosInversos.Add(_Asientos[Num_Asiento].transform);
            for (int i = indiceActual - 1; i >= 0; i--)
            {
                puntosInversos.Add(puntos[i]);
            }
            
            _Sentado = false;
            _Bajando = true;
            indiceBajada = 0;
            animaciones.Play("Walk");
            _Asientos[Num_Asiento].lugaresDisponibles++;
            _Asientos[Num_Asiento].gameObject.SetActive(true);
        }

        if (indiceBajada >= puntosInversos.Count) return;

        Transform destino = puntosInversos[indiceBajada];
        float distancia = Vector3.Distance(transform.position, destino.position);

        if (distancia > 0.05f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                destino.position,
                velocidad * Time.deltaTime
            );
            transform.LookAt(destino);
        }
        else
        {
            indiceBajada++;
            if (indiceBajada >= puntosInversos.Count)
            {
                animaciones.Play("Idle");
                _Bajando = false;
                _BajarActivado = false;
                _Activo = false;
                _ViAsiento = false;
                indiceActual = 0;
                CamionManager.instancia.pasajerosABordo.Remove(this);
                gameObject.SetActive(false);
            }
        }
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