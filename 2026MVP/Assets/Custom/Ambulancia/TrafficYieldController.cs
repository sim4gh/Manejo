using System.Collections.Generic;
using UnityEngine;
using Gley.TrafficSystem;

namespace TlaxcalaSim.Emergency
{
    /// <summary>
    /// Gestiona el orillado de vehículos IA de Gley para ceder el paso a la ambulancia.
    /// 
    /// ENFOQUE: solo aplicar offset lateral + velocidad reducida + intermitentes.
    /// NO usar StopVehicleDriving (eso impide que se orillen visualmente).
    /// 
    /// La clave para que se vea bien al regresar al carril es ESPERAR a que la
    /// ambulancia esté lejos antes de quitar el offset. Si lo quitamos cuando aún
    /// está cerca, el carro intenta regresar al centro mientras la ambulancia
    /// sigue al lado, y se ve raro.
    /// 
    /// FLUJO:
    /// 1. Trigger ENTER: offset hacia la derecha + velocidad reducida + intermitentes.
    /// 2. Trigger EXIT: NO hacemos nada todavía, el carro queda en estado "esperando".
    /// 3. Cuando la ambulancia está a 'restoreDistance' del carro: quitamos offset,
    ///    restauramos velocidad y apagamos intermitentes. El carro retoma su carril
    ///    con espacio libre para hacerlo naturalmente.
    /// 
    /// Pegar este componente en un GameObject vacío llamado "ManagerAmbulancias".
    /// </summary>
    public class TrafficYieldController : MonoBehaviour
    {
        [Header("Comportamiento de yield")]
        [Tooltip("Desplazamiento lateral en metros. POSITIVO = derecha, NEGATIVO = izquierda. " +
                 "Si los carros se orillan al carril contrario, invierte el signo.")]
        [SerializeField] private float lateralOffset = 2.0f;

        [Tooltip("Porcentaje de reducción de velocidad mientras están orillados (0-100). " +
                 "50 = mitad de velocidad. 0 = no reducir.")]
        [Range(0f, 100f)]
        [SerializeField] private float speedReductionPercent = 50f;

        [Tooltip("Activar luces intermitentes mientras están orillados.")]
        [SerializeField] private bool enableHazardLights = true;

        [Header("Cuándo restaurar (distancia)")]
        [Tooltip("Distancia mínima entre la ambulancia y el carro para empezar a restaurarlo. " +
                 "Hasta que la ambulancia se aleje más que esto, el carro sigue orillado.")]
        [SerializeField] private float restoreDistance = 35f;

        [Header("Red de seguridad")]
        [Tooltip("Si por alguna razón un carro nunca llega a la distancia de restauración " +
                 "(ej. la ambulancia se detuvo), forzar restauración después de este tiempo.")]
        [SerializeField] private float forceRestoreAfterSeconds = 20f;

        // Carros que están dentro del trigger AHORA.
        private readonly HashSet<int> _activeYielding = new HashSet<int>();

        // Carros que salieron del trigger pero siguen orillados esperando la distancia segura.
        // Value = tiempo transcurrido desde que salieron (para la red de seguridad).
        private readonly Dictionary<int, float> _waitingElapsedTime = new Dictionary<int, float>();

        // Posición actualizada de la ambulancia (la setea el trigger).
        private Vector3 _ambulancePosition;
        private bool _hasAmbulancePosition;

        public void UpdateAmbulancePosition(Vector3 position)
        {
            _ambulancePosition = position;
            _hasAmbulancePosition = true;
        }

        private void Update()
        {
            ProcessWaitingVehicles();
        }

        private void ProcessWaitingVehicles()
        {
            if (_waitingElapsedTime.Count == 0) return;

            List<int> toRestore = new List<int>();
            List<int> waitingIndices = new List<int>(_waitingElapsedTime.Keys);

            foreach (int vehicleIndex in waitingIndices)
            {
                _waitingElapsedTime[vehicleIndex] += Time.deltaTime;

                VehicleComponent vc = API.GetVehicleComponent(vehicleIndex);
                if (vc == null)
                {
                    // Carro despawneado por Gley.
                    toRestore.Add(vehicleIndex);
                    continue;
                }

                float distance = float.MaxValue;
                if (_hasAmbulancePosition)
                {
                    distance = Vector3.Distance(vc.transform.position, _ambulancePosition);
                }

                bool shouldRestore = distance >= restoreDistance
                                  || _waitingElapsedTime[vehicleIndex] >= forceRestoreAfterSeconds;

                if (shouldRestore)
                {
                    toRestore.Add(vehicleIndex);
                }
            }

            foreach (int idx in toRestore)
            {
                FullyRestoreVehicle(idx);
            }
        }

        /// <summary>
        /// Hace que un vehículo IA empiece a orillarse.
        /// </summary>
        public void MakeVehicleYield(int vehicleIndex)
        {
            if (vehicleIndex < 0) return;

            // Si estaba en estado de espera, regresarlo a estado activo.
            if (_waitingElapsedTime.ContainsKey(vehicleIndex))
            {
                _waitingElapsedTime.Remove(vehicleIndex);
                _activeYielding.Add(vehicleIndex);
                return;
            }

            // Si ya estaba activo, no hacer nada.
            if (_activeYielding.Contains(vehicleIndex)) return;

            _activeYielding.Add(vehicleIndex);

            // Aplicar offset lateral.
            API.SetOffset(vehicleIndex, lateralOffset);

            // Reducir velocidad.
            API.SetSpeedVariationPercentage(vehicleIndex, speedReductionPercent, 0f);

            if (enableHazardLights)
            {
                API.SetHazardLights(vehicleIndex, true);
            }
        }

        /// <summary>
        /// El carro sale del trigger. Pasa al estado "esperando" pero MANTIENE 
        /// offset y velocidad reducida hasta que la ambulancia esté lo suficientemente lejos.
        /// </summary>
        public void RestoreVehicle(int vehicleIndex, Vector3 ambulancePosition)
        {
            if (!_activeYielding.Contains(vehicleIndex)) return;

            _activeYielding.Remove(vehicleIndex);
            _waitingElapsedTime[vehicleIndex] = 0f;

            UpdateAmbulancePosition(ambulancePosition);
        }

        public void RestoreVehicle(int vehicleIndex)
        {
            RestoreVehicle(vehicleIndex, _ambulancePosition);
        }

        /// <summary>
        /// Quita el offset, restaura velocidad y apaga intermitentes.
        /// Se llama cuando la ambulancia ya está lo suficientemente lejos.
        /// </summary>
        private void FullyRestoreVehicle(int vehicleIndex)
        {
            API.SetOffset(vehicleIndex, 0f);
            API.SetSpeedVariationPercentage(vehicleIndex, 0f, 0f);

            if (enableHazardLights)
            {
                API.SetHazardLights(vehicleIndex, false);
            }

            _waitingElapsedTime.Remove(vehicleIndex);
            _activeYielding.Remove(vehicleIndex);
        }

        /// <summary>
        /// Restaura TODOS los vehículos a la vez. Útil al apagar la sirena.
        /// </summary>
        public void RestoreAllVehicles()
        {
            List<int> allIndices = new List<int>();
            allIndices.AddRange(_activeYielding);
            allIndices.AddRange(_waitingElapsedTime.Keys);

            foreach (int idx in allIndices)
            {
                FullyRestoreVehicle(idx);
            }
        }

        private void OnDestroy()
        {
            // Solo limpiar diccionarios sin llamar a la API.
            // Al cerrar Play, TrafficManager puede estar destruido.
            _activeYielding.Clear();
            _waitingElapsedTime.Clear();
        }
    }
}