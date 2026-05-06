using Gley.TrafficSystem;
using Gley.UrbanSystem;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
using UnityEngine.SceneManagement;

namespace Gley.UrbanExample
{
    public class UrbanExample : MonoBehaviour
    {
        [SerializeField] private Transform _busStops;
        private bool _pathSet;
        private int _stopNumber;
        private bool _followVehicle;
        private Transform _player;

        private const int _vehicleToFollow = 23;

        private void Start()
        {
            // Player puede estar inactivo al cargar la escena: SpawnLocationManager
            // lo activa después de teletransportarlo al waypoint. GameObject.Find
            // ignora inactivos → resolver perezoso en Update cuando se necesite.
            ResolvePlayer();
        }

        private void ResolvePlayer()
        {
            if (_player != null) return;
            var p = GameObject.Find("Player");
            if (p == null) p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.transform;
        }

        //every time a destination is reached, a new one is selected
        private void BusStationReached(int vehicleIndex)
        {
            //remove listener otherwise this method will be called on each frame
            TrafficSystem.Events.OnDestinationReached -= BusStationReached;
            if (vehicleIndex == 0)
            {
                _stopNumber++;
                if (_stopNumber == _busStops.childCount)
                {
                    _stopNumber = 0;
                }
                //stop and wait for 5 seconds, then move to the next destination
                Invoke("ContinueDriving", 5);
            }
        }

        /// <summary>
        /// Continue on path
        /// </summary>
        private void ContinueDriving()
        {
            TrafficSystem.Events.OnDestinationReached += BusStationReached;
            TrafficSystem.API.SetDestination(0, _busStops.GetChild(_stopNumber).transform.position);
        }

        private void Update()
        {
            if (!_pathSet)
            {
                if (TrafficSystem.API.IsInitialized())
                {
                    _pathSet = true;
                    SetPath();
                }
            }

            if (GetKeyDownF())
            {
                _followVehicle = !_followVehicle;
                if (_followVehicle)
                {
                    GameObject.Find("Main Camera").GetComponent<CameraFollow>().target = API.GetVehicleComponent(_vehicleToFollow).transform;
                    API.SetCamera(API.GetVehicleComponent(_vehicleToFollow).transform);
                }
                else
                {
                    ResolvePlayer();
                    GameObject.Find("Main Camera").GetComponent<CameraFollow>().target = _player;
                    API.SetCamera(_player);
                }
            }

            if (GetKeyDownESC())
            {
                Application.Quit();
            }

            if (GetKeyDownR())
            {
                SceneManager.LoadScene(0);
            }
        }

        /// <summary>
        /// set a path towards destination
        /// </summary>
        private void SetPath()
        {
            var vehicleComponent = TrafficSystem.API.GetVehicleComponent(0);
            if (vehicleComponent.gameObject.activeSelf)
            {
                TrafficSystem.Events.OnDestinationReached += BusStationReached;
                TrafficSystem.API.SetDestination(0, _busStops.GetChild(_stopNumber).transform.position);
            }
            else
            {
                Invoke("SetPath", 1);
            }
        }

        private bool GetKeyDownF()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }

        private bool GetKeyDownR()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        private bool GetKeyDownESC()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        //remove listeners
        private void OnDestroy()
        {
            TrafficSystem.Events.OnDestinationReached -= BusStationReached;
        }
    }
}
