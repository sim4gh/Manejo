using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gley.UrbanSystem;
#if GLEY_TRAFFIC_SYSTEM
using VehicleTypes = Gley.TrafficSystem.User.VehicleTypes;
#else
using VehicleTypes = Gley.TrafficSystem.VehicleTypes;
#endif

namespace Gley.TrafficSystem
{
    /// <summary>
    /// Add this script on a vehicle prefab and configure the required parameters
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [HelpURL("https://gley.gitbook.io/mobile-traffic-system-v3/setup-guide/vehicle-implementation")]
    public class VehicleComponent : MonoBehaviour, ITrafficParticipant,IColliderRemovalListener
    {
        [Header("Object References")]
        [Tooltip("RigidBody of the vehicle")]
        public Rigidbody rb;
        [Tooltip("Empty GameObject used to rotate the vehicle from the correct point")]
        public Transform carHolder;
        [Tooltip("Front trigger used to detect obstacle. It is automatically generated")]
        public Transform frontTrigger;
        [Tooltip("Assign this object if you need a hard shadow on your vehicle, leave it blank otherwise")]
        public Transform shadowHolder;
        [Tooltip("A transform representing the front of your vehicle")]
        public Transform _frontPosition;
        [Tooltip("A transform representing the back of your vehicle")]
        public Transform _backPosition;



        [Header("Wheels")]
        [Tooltip("All vehicle wheels and their properties")]
        public Wheel[] allWheels;
        [Tooltip("Max wheel turn amount in degrees")]
        public float maxSteer = 30;
        [Tooltip("If suspension is set to 0, the value of suspension will be half of the wheel radius")]
        public float maxSuspension = 0f;
        [Tooltip("How rigid the suspension will be. Higher the value -> more rigid the suspension")]
        public float springStiffness = 5;
        [Tooltip("Factor used to calculate the leaning of the vehicle when turning. 0 = no leaning, 1 = max leaning")]
        [Range(0, 1)]
        [SerializeField] float sideLeaningFactor = 0.5f;
        [Tooltip("Factor used to calculate the forward leaning of the vehicle when accelerating/braking. 0 = no leaning, 1 = max leaning")]
        [Range(0, 1)]
        [SerializeField] float forwardLeaningFactor = 0.5f;


        [Header("Car Properties")]
        [Tooltip("Vehicle type used for making custom paths")]
        public VehicleTypes vehicleType;
        [Tooltip("Min vehicle speed. Actual vehicle speed is picked random between min and max")]
        public int minPossibleSpeed = 40;
        [Tooltip("Max vehicle speed")]
        public int maxPossibleSpeed = 90;
        [Tooltip("Time in seconds to reach max speed (acceleration)")]
        public float accelerationTime = 10;
        [Tooltip("Time in seconds to stop from max speed")]
        public float brakeTime = 10;
        [Tooltip("Time in seconds to turn to maxSteer")]
        public float steeringTime = 1.5f;
        [Tooltip("Distance to keep from an obstacle/vehicle")]
        public float distanceToStop = 3;
        [Tooltip("Car starts braking when an obstacle enters trigger. Total length of the trigger = distanceToStop+minTriggerLength")]
        public float triggerLength = 4;

        [HideInInspector]
        public bool updateTrigger = false;
        [HideInInspector]
        public float maxTriggerLength = 10;
        [HideInInspector]
        public TrailerComponent trailer;
        [HideInInspector]
        public Transform trailerConnectionPoint;
        [HideInInspector]
        public float length = 0;
        [HideInInspector]
        public float coliderHeight = 0;
        [HideInInspector]
        public float wheelDistance;
        [HideInInspector]
        public VisibilityScript visibilityScript;

        private Collider[] _allColliders;
        private List<Obstacle> _obstacleList;
        private Transform _frontAxle;
        private BoxCollider _frontCollider;
        private ModifyTriggerSize _modifyTriggerSize;
        private EngineSoundComponent _engineSound;
        private BlinkersController _blinkersController;
        private MovementInfo _movementInfo;
        private LayerMask _buildingLayers;
        private LayerMask _obstacleLayers;
        private LayerMask _playerLayers;
        private LayerMask _roadLayers;
        private IVehicleLightsComponent _vehicleLights;
        private float _springForce;
        private float _maxSpeedMS;
        private float _storedMaxSpeed;
        private float _minTriggerLength;
        private float _colliderWidth;
        private float _powerStep;
        private float _brakeStep;
        private float _acceleration;
        private float _steerStep;
        private float _steerAngle;
        private int _listIndex;
        private bool _lightsOn;
        private bool _ignored;

        public Collider[] AllColliders => _allColliders;
        public List<Obstacle> Obstacles => _obstacleList;
        public BlinkersController BlinkersController => _blinkersController;
        public Transform FrontTrigger => frontTrigger;
        public MovementInfo MovementInfo => _movementInfo;
        public VehicleTypes VehicleType => vehicleType;
        public float ColliderWidth => _colliderWidth;
        public float MaxSpeed => _maxSpeedMS;
        public float SpringForce => _springForce;
        public float MaxSteer => maxSteer;
        public float PowerStep => _powerStep;
        public float BrakeStep => _brakeStep;
        public float SteerStep => _steerStep;
        public float SteerAngle => _steerAngle;
        public float SpringStiffness => springStiffness;
        public float SideLeaningFactor => sideLeaningFactor;
        public float ForwardLeaningFactor => forwardLeaningFactor;
        public int ListIndex => _listIndex;


        public Transform BackPosition
        {
            get
            {
                if (_backPosition == null)
                {
                    _backPosition = transform;
                }
                return _backPosition;
            }
        }

        public Transform FrontPosition
        {
            get
            {
                if (_frontPosition == null)
                {
                    _frontPosition = transform;
                }
                return _frontPosition;
            }
        }
        public bool Ignored
        {
            get { return _ignored; }
            set { _ignored = value; }
        }
        public bool HasTrailer
        {
            get
            {
                return trailer != null;
            }
        }


        /// <summary>
        /// Initialize vehicle
        /// </summary>
        /// <param name="buildingLayers">static colliders to interact with</param>
        /// <param name="obstacleLayers">dynamic colliders to interact with</param>
        /// <param name="playerLayers">player colliders to interact with</param>
        /// <returns>the vehicle</returns>
        public virtual VehicleComponent Initialize(LayerMask buildingLayers, LayerMask obstacleLayers, LayerMask playerLayers, LayerMask roadLayers, bool lightsOn, ModifyTriggerSize modifyTriggerSize, TrafficWaypointsData trafficWaypointsData, int vehicleIndex, bool ignored, float minOffset, float maxOffset)
        {
            _ignored = ignored;
            _buildingLayers = buildingLayers;
            _obstacleLayers = obstacleLayers;
            _playerLayers = playerLayers;
            _roadLayers = roadLayers;
            _modifyTriggerSize = modifyTriggerSize;
            _allColliders = GetComponentsInChildren<Collider>();
            _springForce = ((rb.mass * -Physics.gravity.y) / allWheels.Length);

            _frontCollider = frontTrigger.GetChild(0).GetComponent<BoxCollider>();
            _colliderWidth = _frontCollider.size.x;
            _minTriggerLength = _frontCollider.size.z;
            _frontAxle = new GameObject("FrontAxle").transform;
            _frontAxle.transform.SetParent(frontTrigger.parent);
            _frontAxle.transform.position = frontTrigger.position;
            DeactivateVehicle();

            //compute center of mass based on the wheel position
            Vector3 centerOfMass = Vector3.zero;
            for (int i = 0; i < allWheels.Length; i++)
            {
                allWheels[i].wheelTransform.Translate(Vector3.up * (allWheels[i].maxSuspension / 2 + allWheels[i].wheelRadius));
                centerOfMass += allWheels[i].wheelTransform.position;
            }
            rb.centerOfMass = centerOfMass / allWheels.Length;

            //set additional components
            _engineSound = GetComponent<EngineSoundComponent>();
            if (_engineSound)
            {
                _engineSound.Initialize();
            }

            _lightsOn = lightsOn;
            _vehicleLights = GetComponent<VehicleLightsComponent>();
            if (_vehicleLights == null)
            {
                _vehicleLights = GetComponent<VehicleLightsComponentV2>();
            }
            if (_vehicleLights != null)
            {
                _vehicleLights.Initialize();
            }

            if (trailer != null)
            {
                trailer.Initialize(this);
            }

            _listIndex = vehicleIndex;
            _movementInfo = new MovementInfo(_listIndex, ColliderWidth, Random.Range(minOffset, maxOffset));

            _blinkersController = new BlinkersController(_movementInfo, trafficWaypointsData, _vehicleLights);
            _steerStep = maxSteer / steeringTime * Time.fixedDeltaTime;

            return this;
        }


        /// <summary>
        /// Check for collisions
        /// </summary>
        /// <param name="collision"></param>
        protected virtual void OnCollisionEnter(Collision collision)
        {
            Events.TriggerVehicleCrashEvent(_listIndex, GetObstacleTypes(collision.collider), collision.collider);
        }


        /// <summary>
        /// Remove a collider from the list
        /// </summary>
        /// <param name="other"></param>
        protected virtual void OnTriggerExit(Collider other)
        {
            // TLAX PATCH: espejo de OnTriggerEnter. Antes solo procesaba !isTrigger, así
            // que los triggers "PlayerTrigger" (moto del jugador, burbuja del peatón) que
            // OnTriggerEnter SÍ agrega a _obstacleList nunca se removían → el carro quedaba
            // frenado para siempre. Re-aplicar tras cualquier reimport de Gley.
            if (ShouldTrackObstacle(other.isTrigger, other.gameObject.CompareTag("PlayerTrigger")))
            {
                //TODO this should only trigger if objects of interest are doing trigger exit
                if (other.gameObject.layer == gameObject.layer ||
                    (_buildingLayers == (_buildingLayers | (1 << other.gameObject.layer))) ||
                    (_obstacleLayers == (_obstacleLayers | (1 << other.gameObject.layer))) ||
                    (_playerLayers == (_playerLayers | (1 << other.gameObject.layer))))
                {
                    for (int i = _obstacleList.Count - 1; i >= 0; i--)
                    {
                        if (_obstacleList[i].Collider == other)
                        {
                            var obstacle = _obstacleList[i];
                            _obstacleList.RemoveAt(i);
                            VehicleEvents.TriggerObstacleInTriggerRemovedEvent(_listIndex, obstacle);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// CHeck trigger objects
        /// </summary>
        /// <param name="other"></param>
        protected virtual void OnTriggerEnter(Collider other)
        {
            // TLAX PATCH: además de colliders sólidos, trata los triggers con tag
            // "PlayerTrigger" como obstáculos (moto del jugador / burbuja de rango
            // extendido del peatón). El removal correspondiente vive en OnTriggerExit.
            if (ShouldTrackObstacle(other.isTrigger, other.gameObject.CompareTag("PlayerTrigger")))
            {
                NewColliderHit(other);
            }
        }

        /// <summary>
        /// TLAX PATCH: predicado puro y testeable que decide si un collider debe entrar/salir
        /// de _obstacleList. Un collider cuenta si es sólido (no-trigger) o si es un trigger
        /// etiquetado "PlayerTrigger". Enter y Exit DEBEN usar el mismo predicado (simetría)
        /// para que el carro reanude cuando el obstáculo se retira.
        /// </summary>
        public static bool ShouldTrackObstacle(bool isTrigger, bool isPlayerTrigger)
        {
            return !isTrigger || isPlayerTrigger;
        }


        public virtual void ApplyAdditionalForces(float wheelTurnAngle)
        {
            _steerAngle = wheelTurnAngle;
        }

        public void SetVelocity(Vector3 initialVelocity, Vector3 angularVelocity)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = initialVelocity;
#else
            rb.velocity = initialVelocity;
#endif
            rb.angularVelocity = angularVelocity;
        }


        /// <summary>
        /// Apply new trigger size delegate
        /// </summary>
        /// <param name="triggerSizeModifier"></param>
        public void SetTriggerSizeModifierDelegate(ModifyTriggerSize triggerSizeModifier)
        {
            _modifyTriggerSize = triggerSizeModifier;
        }


        /// <summary>
        /// Add a vehicle on scene
        /// </summary>
        /// <param name="position"></param>
        /// <param name="vehicleRotation"></param>
        /// <param name="masterVolume"></param>
        public virtual void ActivateVehicle(Vector3 position, Quaternion vehicleRotation, Quaternion trailerRotation)
        {
            _storedMaxSpeed = Random.Range(minPossibleSpeed, maxPossibleSpeed);

            _maxSpeedMS = _storedMaxSpeed.KMHToMS();

            int nrOfFrames = (int)(accelerationTime / Time.fixedDeltaTime);
            _powerStep = MaxSpeed / nrOfFrames;

            _acceleration = _powerStep / Time.fixedDeltaTime;

            nrOfFrames = (int)(brakeTime / Time.fixedDeltaTime);
            _brakeStep = MaxSpeed / nrOfFrames;

            gameObject.transform.SetPositionAndRotation(position, vehicleRotation);

            //position vehicle with front wheels on the waypoint
            float distance = Vector3.Distance(position, frontTrigger.transform.position);
            transform.Translate(-transform.forward * distance, Space.World);

            if (trailer != null)
            {
                trailer.transform.rotation = trailerRotation;
            }

            gameObject.SetActive(true);

            ColliderRemovalRegistry.Register(this);

            if (_engineSound)
            {
                _engineSound.Play(0);
            }

            SetMainLights(_lightsOn);
        }


        /// <summary>
        /// Remove a vehicle from scene
        /// </summary>
        public virtual void DeactivateVehicle()
        {
            gameObject.SetActive(false);
            ColliderRemovalRegistry.Unregister(this);
            _obstacleList = new List<Obstacle>();
            visibilityScript.Reset();

            if (_engineSound)
            {
                _engineSound.Stop();
            }

            _vehicleLights?.DeactivateLights();

            if (trailer)
            {
                trailer.DeactivateVehicle();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns>Max RayCast length</returns>
        public float GetRayCastLength()
        {
            return allWheels[0].raycastLength;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns>Wheel circumference</returns>
        public float GetWheelCircumference()
        {
            return allWheels[0].wheelCircumference;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns>Vehicle velocity vector</returns>
        public Vector3 GetVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }


        /// <summary>
        /// Returns current speed in m/s
        /// </summary>
        /// <returns></returns>
        public float GetCurrentSpeedMS()
        {
            return GetVelocity().magnitude;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns>Trigger orientation</returns>
        public Vector3 GetHeading()
        {
            return frontTrigger.forward;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns>vehicle orientation</returns>
        public Vector3 GetForwardVector()
        {
            return transform.forward;
        }


        /// <summary>
        /// Check if the vehicle is not in view
        /// </summary>
        /// <returns></returns>
        public bool CanBeRemoved()
        {
            return visibilityScript.IsNotInView();
        }


        public Vector3 GetFrontAxleUpVector()
        {
            return _frontAxle.up;
        }


        public Vector3 GetFrontAxleForwardVector()
        {
            return _frontAxle.forward;
        }


        public Vector3 GetFrontAxleRightVector()
        {
            return _frontAxle.right;
        }


        public Vector3 GetFrontAxlePosition()
        {
            return _frontAxle.position;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns>number of vehicle wheels</returns>
        public int GetNrOfWheels()
        {
            return allWheels.Length;
        }


        /// <summary>
        /// Returns the nr of wheels of the trailer
        /// </summary>
        /// <returns></returns>
        public int GetTrailerWheels()
        {
            if (trailer == null)
            {
                return 0;
            }
            return trailer.GetNrOfWheels();
        }


        /// <summary>
        /// Check if current collider is from a new object
        /// </summary>
        /// <param name="colliders"></param>
        /// <returns></returns>
        public bool AlreadyCollidingWith(Collider[] colliders)
        {
            for (int i = 0; i < _obstacleList.Count; i++)
            {
                for (int j = 0; j < colliders.Length; j++)
                {
                    if (_obstacleList[i].Collider == colliders[j])
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        /// <summary>
        /// Remove a collider from the trigger if the collider was destroyed
        /// </summary>
        /// <param name="collider"></param>
        public void ColliderRemoved(Collider collider)
        {
            if (_obstacleList != null)
            {
                if (_obstacleList.Any(cond => cond.Collider == collider))
                {
                    OnTriggerExit(collider);
                }
            }
        }


        /// <summary>
        /// Removed a list of colliders from the trigger if the colliders ware destroyed
        /// </summary>
        /// <param name="colliders"></param>
        public void ColliderRemoved(Collider[] colliders)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (_obstacleList.Any(cond => cond.Collider == colliders[i]))
                {
                    OnTriggerExit(colliders[i]);
                }
            }
        }


        //update the lights component if required
        #region Lights
        internal void SetMainLights(bool on)
        {
            if (on != _lightsOn)
            {
                _lightsOn = on;
            }
            if (_vehicleLights != null)
            {
                _vehicleLights.SetMainLights(on);
            }
        }


        public void SetReverseLights(bool active)
        {
            if (_vehicleLights != null)
            {
                _vehicleLights.SetReverseLights(active);
            }
        }


        public void SetBrakeLights(bool active)
        {
            if (_vehicleLights != null)
            {
                _vehicleLights.SetBrakeLights(active);
            }
        }


        public virtual void SetBlinker(BlinkType blinkType)
        {
            if (_vehicleLights != null)
            {
                _vehicleLights.SetBlinker(blinkType);
            }
        }


        public void UpdateLights(float realtimeSinceStartup)
        {
            if (_vehicleLights != null)
            {
                _vehicleLights.UpdateLights(realtimeSinceStartup);
            }
        }
        #endregion


        //update the sound component if required
        #region Sound
        public void UpdateEngineSound(float masterVolume)
        {
            if (_engineSound)
            {
                _engineSound.UpdateEngineSound(GetCurrentSpeedMS(), MaxSpeed, masterVolume);
            }
        }
        #endregion


        /// <summary>
        /// Modify the dimension of the front trigger
        /// </summary>
        public void UpdateColliderSize()
        {
            if (updateTrigger)
            {
                _modifyTriggerSize?.Invoke(GetVelocity().magnitude * 3.6f, _frontCollider, _storedMaxSpeed, _minTriggerLength, maxTriggerLength);
            }
        }


        public virtual void UpdateVehicleScripts(float volume, float realTimeSinceStartup, bool reverseLightsOn)
        {
            UpdateEngineSound(volume);
            UpdateLights(realTimeSinceStartup);
            UpdateColliderSize();
            SetReverseLights(reverseLightsOn);
        }


        public float GetTimeToCoverDistance(float distance)
        {
            // Calculate time and distance to reach max speed
            float timeToMaxSpeed = (MaxSpeed - GetCurrentSpeedMS()) / _acceleration;
            float distanceToMaxSpeed = (GetCurrentSpeedMS() * timeToMaxSpeed) + (0.5f * _acceleration * timeToMaxSpeed * timeToMaxSpeed);

            if (distanceToMaxSpeed >= distance)
            {
                // The vehicle can reach the target before hitting max speed
                return (-GetCurrentSpeedMS() + Mathf.Sqrt(GetCurrentSpeedMS() * GetCurrentSpeedMS() + 2 * _acceleration * distance)) / _acceleration;
            }
            else
            {
                // The vehicle hits max speed, calculate remaining distance
                float remainingDistance = distance - distanceToMaxSpeed;
                float timeAtMaxSpeed = remainingDistance / MaxSpeed;

                return timeToMaxSpeed + timeAtMaxSpeed;
            }
        }


        /// <summary>
        /// Every time a new collider is hit it is added inside the list
        /// </summary>
        /// <param name="other"></param>
        private void NewColliderHit(Collider other)
        {
            ObstacleTypes obstacleType = GetObstacleTypes(other);

            if (obstacleType != ObstacleTypes.Other && obstacleType != ObstacleTypes.Road)
            {
                if (!_obstacleList.Any(cond => cond.Collider == other))
                {
                    bool isConvex = true;
                    if (other is MeshCollider meshCollider)
                    {
                        isConvex = meshCollider.convex;
                    }

                    ITrafficParticipant component = null;
                    if (obstacleType == ObstacleTypes.TrafficVehicle || obstacleType == ObstacleTypes.Player)
                    {
                        Rigidbody otherRb = other.attachedRigidbody;
                        if (otherRb != null)
                        {
                            component = otherRb.GetComponent<ITrafficParticipant>();
                        }
                    }

                    _obstacleList.Add(new Obstacle(other, isConvex, obstacleType, component));
                    VehicleEvents.TriggerObstacleInTriggerAddedEvent(_listIndex, _obstacleList[_obstacleList.Count - 1]);
                }
            }
        }


        /// <summary>
        /// Returns the type of obstacle that just entered the front trigger
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        private ObstacleTypes GetObstacleTypes(Collider other)
        {
            bool carHit = other.gameObject.layer == gameObject.layer;
            //possible vehicle hit
            if (carHit)
            {
                Rigidbody otherRb = other.attachedRigidbody;
                if (otherRb != null)
                {
                    if (otherRb.GetComponent<ITrafficParticipant>() != null)
                    {
                        return ObstacleTypes.TrafficVehicle;
                    }
                }
                //if it is on traffic layer but it lacks a vehicle component, it is a dynamic object
                return ObstacleTypes.DynamicObject;
            }
            else
            {
                //trigger the corresponding event based on object layer
                if (_buildingLayers == (_buildingLayers | (1 << other.gameObject.layer)))
                {
                    return ObstacleTypes.StaticObject;
                }
                else
                {
                    if (_obstacleLayers == (_obstacleLayers | (1 << other.gameObject.layer)))
                    {
                        return ObstacleTypes.DynamicObject;
                    }
                    else
                    {
                        if (_playerLayers == (_playerLayers | (1 << other.gameObject.layer)))
                        {
                            return ObstacleTypes.Player;
                        }
                        else
                        {
                            if (_roadLayers == (_roadLayers | (1 << other.gameObject.layer)))
                            {
                                return ObstacleTypes.Road;
                            }
                        }
                    }
                }
            }
            return ObstacleTypes.Other;
        }

        public bool IsInitialized()
        {
            return API.IsInitialized();
        }

        public void TriggerColliderRemoved(Collider[] colliders)
        {
            API.TriggerColliderRemovedEvent(colliders);
        }
    }
}