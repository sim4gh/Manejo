using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gley.UrbanSystem;

/// <summary>
/// Variante de ParadaManager para la escena BusPasajeros: el conductor abre
/// y cierra la puerta con un botón (toggle), y suena DoorOpen/DoorClose.wav.
/// Reemplaza al ParadaManager original SOLO en los GameObjects de paradas
/// del bus. CamionDCarga sigue usando ParadaManager con auto-board.
///
/// Diseño (post codex 2026-05-03):
/// - Lectura de input en Update(), no en OnTriggerStay (evita perder pulsos).
/// - Velocidad numérica del Rigidbody (no string del HUD).
/// - Toggle: 1ª pulsación abre + SFX, 2ª cierra + SFX. Sin autoclose por velocidad.
/// - Cierre auto solo al salir del trigger (cleanup, no comportamiento de juego).
/// - Coroutines de cola se cancelan al cerrar (no quedan flags stale en cierre+
///   reapertura rápido).
/// </summary>
public class ParadaManagerBus : MonoBehaviour
{
    public List<Pasajero> pasajero;
    public int NumParada;
    public SimpleSpeedGauge movimientoCarro;

    [Tooltip("Threshold km/h para considerar el bus 'detenido'")]
    public float stopSpeedThreshold = 1f;

    private Rigidbody _rb;
    private UIInputNew _uiInput;
    private bool _inStopZone;
    private bool _doorOpen;
    private bool _doorPrev;
    private Coroutine _boardingCo;
    private Coroutine _alightingCo;

    void Start()
    {
        foreach (Transform p in transform)
        {
            var pj = p.GetComponent<Pasajero>();
            if (pj != null) pasajero.Add(pj);
        }
        _uiInput = Object.FindAnyObjectByType<UIInputNew>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("puerta")) return;
        _inStopZone = true;
        if (_rb == null && other.attachedRigidbody != null) _rb = other.attachedRigidbody;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("puerta")) return;
        _inStopZone = false;
        if (_doorOpen) CloseDoor();
    }

    void Update()
    {
        if (!_inStopZone) return;
        if (_uiInput == null) _uiInput = Object.FindAnyObjectByType<UIInputNew>();
        if (_uiInput == null) return;

        bool now = _uiInput.IsDoorPressed();
        bool edge = now && !_doorPrev;
        _doorPrev = now;
        if (!edge) return;

        if (!_doorOpen && IsStopped())
        {
            OpenDoor();
        }
        else if (_doorOpen)
        {
            CloseDoor();
        }
    }

    bool IsStopped()
    {
        if (_rb != null)
        {
            float kmh = _rb.linearVelocity.magnitude * 3.6f;
            return kmh < stopSpeedThreshold;
        }
        return movimientoCarro != null && movimientoCarro.velocidadActual == "0";
    }

    void OpenDoor()
    {
        _doorOpen = true;
        if (DoorSFXManager.Instance != null) DoorSFXManager.Instance.PlayDoorOpen();
        if (_boardingCo != null) StopCoroutine(_boardingCo);
        if (_alightingCo != null) StopCoroutine(_alightingCo);
        _boardingCo = StartCoroutine(ColaPasaje());
        _alightingCo = StartCoroutine(ColaBajada());
    }

    void CloseDoor()
    {
        _doorOpen = false;
        if (DoorSFXManager.Instance != null) DoorSFXManager.Instance.PlayDoorClose();
        if (_boardingCo != null) { StopCoroutine(_boardingCo); _boardingCo = null; }
        if (_alightingCo != null) { StopCoroutine(_alightingCo); _alightingCo = null; }
    }

    public bool IsPromptVisible() => _inStopZone && !_doorOpen && IsStopped();

    IEnumerator ColaPasaje()
    {
        for (int i = 0; i < pasajero.Count; i++)
        {
            yield return new WaitForSeconds(3);
            if (!_doorOpen) yield break;
            pasajero[i]._Activo = true;
        }
        _boardingCo = null;
    }

    IEnumerator ColaBajada()
    {
        var abordo = CamionManager.instancia.pasajerosABordo;
        for (int i = abordo.Count - 1; i >= 0; i--)
        {
            if (abordo[i]._Sentado && abordo[i].Mi_Parada == NumParada)
            {
                yield return new WaitForSeconds(3);
                if (!_doorOpen) yield break;
                abordo[i]._BajarActivado = true;
            }
        }
        _alightingCo = null;
    }
}
