using UnityEngine;
using Gley.TrafficSystem;

namespace TlaxcalaSim.Emergency
{
    /// <summary>
    /// Componente que va en la ambulancia. Detecta vehículos IA de Gley dentro de
    /// un BoxCollider trigger y les ordena orillarse cuando la sirena está activa.
    /// 
    /// Filtrado por componente VehicleComponent (no por layer), porque los carros
    /// de Gley deben compartir layer con el terreno para que su raycasting funcione.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class TriggerAmbulancia : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Controller que gestiona el orillado. Si se deja vacío, se busca por GameObject.Find('ManagerAmbulancias').")]
        [SerializeField] private TrafficYieldController _yieldController;

        [Header("Configuración de detección")]
        [Tooltip("Tamaño del área de detección (X = ancho, Y = alto, Z = largo hacia adelante).")]
        [SerializeField] private Vector3 detectionSize = new Vector3(15f, 5f, 40f);

        [Tooltip("Centro del BoxCollider relativo al GameObject. Z positivo = hacia adelante.")]
        [SerializeField] private Vector3 detectionCenter = new Vector3(0f, 0f, 15f);

        [Tooltip("Si está activo, los carros se orillan. Si no, se ignora la ambulancia.")]
        [SerializeField] private bool sirenActive = true;

        [Header("Activación automática")]
        [SerializeField] private bool autoActivateBySpeed = false;
        [SerializeField] private float autoActivationSpeedKmh = 40f;

        [Header("Input manual")]
        [SerializeField] private KeyCode toggleKey = KeyCode.H;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool verboseLogging = false;

        private BoxCollider _detectionCollider;
        private Rigidbody _ambulanceRb;

        public bool IsSirenActive => sirenActive;

        private void Awake()
        {
            if (_yieldController == null)
            {
                GameObject manager = GameObject.Find("ManagerAmbulancias");
                if (manager != null)
                {
                    _yieldController = manager.GetComponent<TrafficYieldController>();
                }
            }

            _detectionCollider = GetComponent<BoxCollider>();
            _detectionCollider.isTrigger = true;
            _detectionCollider.size = detectionSize;
            _detectionCollider.center = detectionCenter;

            _ambulanceRb = GetComponent<Rigidbody>();
            if (_ambulanceRb == null)
            {
                _ambulanceRb = GetComponentInParent<Rigidbody>();
            }
        }

        private void Start()
        {
            if (_yieldController == null)
            {
                Debug.LogError($"[{nameof(TriggerAmbulancia)}] No se encontró TrafficYieldController. " +
                               "Asegúrate de tener un GameObject llamado 'ManagerAmbulancias' con el script, " +
                               "o asígnalo manualmente en el inspector.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            // Mantener al controller informado de dónde está la ambulancia en cada frame.
            // Esto es necesario para calcular distancias a los carros en estado "waiting".
            _yieldController.UpdateAmbulancePosition(transform.position);

            // Modo de activación automática por velocidad.
            if (autoActivateBySpeed && _ambulanceRb != null)
            {
                float speedKmh = _ambulanceRb.linearVelocity.magnitude * 3.6f;
                sirenActive = speedKmh >= autoActivationSpeedKmh;
            }
            else
            {
                if (Input.GetKeyDown(toggleKey))
                {
                    sirenActive = !sirenActive;
                    if (verboseLogging)
                    {
                        Debug.Log($"[Ambulance] Sirena: {(sirenActive ? "ON" : "OFF")}");
                    }

                    if (!sirenActive)
                    {
                        _yieldController.RestoreAllVehicles();
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!sirenActive) return;
            if (!TryGetGleyVehicleIndex(other, out int vehicleIndex)) return;

            if (verboseLogging)
            {
                Debug.Log($"[Ambulance] Carro IA {vehicleIndex} entró al trigger, ordenando orillarse.");
            }

            _yieldController.MakeVehicleYield(vehicleIndex);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryGetGleyVehicleIndex(other, out int vehicleIndex)) return;

            if (verboseLogging)
            {
                Debug.Log($"[Ambulance] Carro IA {vehicleIndex} salió del trigger, esperando a alejarse para restaurar.");
            }

            _yieldController.RestoreVehicle(vehicleIndex, transform.position);
        }

        private bool TryGetGleyVehicleIndex(Collider other, out int vehicleIndex)
        {
            vehicleIndex = -1;
            VehicleComponent vehicleComp = other.GetComponentInParent<VehicleComponent>();
            if (vehicleComp == null) return false;
            vehicleIndex = API.GetVehicleIndex(vehicleComp.gameObject);
            return vehicleIndex >= 0;
        }

        private void OnValidate()
        {
            if (_detectionCollider == null)
            {
                _detectionCollider = GetComponent<BoxCollider>();
            }
            if (_detectionCollider != null)
            {
                _detectionCollider.size = detectionSize;
                _detectionCollider.center = detectionCenter;
                _detectionCollider.isTrigger = true;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.color = sirenActive ? new Color(1f, 0f, 0f, 0.3f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(detectionCenter, detectionSize);
            Gizmos.color = sirenActive ? Color.red : Color.gray;
            Gizmos.DrawWireCube(detectionCenter, detectionSize);
            Gizmos.matrix = oldMatrix;
        }

        public void SetSirenActive(bool active)
        {
            sirenActive = active;
            if (!active && _yieldController != null)
            {
                _yieldController.RestoreAllVehicles();
            }
        }
    }
}