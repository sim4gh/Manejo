using UnityEngine;

namespace Gley.PedestrianSystem
{
    [HelpURL("https://gley.gitbook.io/mobile-pedestrian-system/setup-guide/initializing-asset")]
    public class PedestrianSystemComponent : MonoBehaviour
    {
        public int NoPeatones;

        [Tooltip("Player is used to instantiate pedestrians out of view")]
        [SerializeField] private Transform _player;

        [Tooltip("Max number of pedestrians")]
        [SerializeField] private int _nrOfPedestrians = 1;

        [Tooltip("List of different pedestrians (Right Click->Create->Pedestrian System->Pedestrian Pool)")]
        [SerializeField] private PedestrianPool _pedestrianPool;

        [Header("Spawning")]
        [Tooltip("Minimum distance from the player where a pedestrian can be instantiated. (If -1 the system will automatically determine this value)")]
        [SerializeField] private float _minDistanceToAdd = -1;

        [Tooltip("Distance from the player where a pedestrian can be removed. (If -1 the system will automatically determine this value)")]
        [SerializeField] private float _distanceToRemove = -1;

        [Header("Density")]
        [Tooltip("Nr of pedestrians instantiated around the player from the start. Set it to something < nrOfPedestrians for low density right at the start. (If -1 all pedestrians will be instantiated from the beginning)")]
        [SerializeField] private int _initialActivePedestrians = -1;

        [Tooltip("Set high priority on paths for higher pedestrian density.")]
        [SerializeField] private bool _useWaypointPriority = false;

        [Header("Waypoints")]
        [Tooltip("The number of known waypoints for the pedestrian")] 
        [SerializeField] private int _defaultPathLength = 2;

        [Tooltip("Area to disable from the start. Pedestrians are not allowed to spawn there")]
        [SerializeField] private Area _disableWaypointsArea = default;

        void Start()
        {
            PedestrianSystemOptions options = new PedestrianSystemOptions()
            {
                DisableWaypointsArea = new Area(_disableWaypointsArea),
                DistanceToRemove = _distanceToRemove,
                InitialDensity = _initialActivePedestrians,
                MinDistanceToAdd = _minDistanceToAdd,
                UseWaypointPriority = _useWaypointPriority,
                DefaultPathLength = _defaultPathLength,
            };

            API.Initialize(_player, _nrOfPedestrians, _pedestrianPool, options);

            NoPeatones = PlayerPrefs.GetInt("NoPeatones", 0);
            _nrOfPedestrians = NoPeatones;
        }
    }
}