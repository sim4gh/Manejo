using Gley.UrbanSystem;
using Gley.UrbanSystem.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gley.PedestrianSystem.Editor
{
    public class PedestrianEditorBridge : IPedestrianEditorBridge, IDisposable
    {
        private PedestrianWaypointEditorData _data;
        private PedestrianWaypointDrawer _drawer;

        private PedestrianWaypointEditorData Data
        {
            get
            {
                if (_data == null)
                {
                    InitializeDrawer();
                }
                return _data;
            }
        }

        private PedestrianWaypointDrawer Drawer
        {
            get
            {
                if (_drawer == null)
                {
                    InitializeDrawer();
                }
                return _drawer;
            }
        }

        public event IPedestrianEditorBridge.WaypointClicked OnWaypointClicked;


        public PedestrianEditorBridge()
        {
            InitializeDrawer();
            
        }

        void InitializeDrawer()
        {
            _data = new PedestrianWaypointEditorData();
            if (_data.GetAllWaypoints().Length == 0)
            {
                //Debug.LogWarning("Pedestrian Editor Bridge not created. No pedestrian waypoints found in the scene.");
                return;
            }
            else
            {
                _drawer = new PedestrianWaypointDrawer(_data);
                _drawer.OnWaypointClicked += InternalWaypointClicked;
            }
        }

        public void AppendGroundLayers(ref LayerMask groundLayers)
        {
            var layers = FileCreator.LoadOrCreateLayers<LayerSetup>(
                PedestrianSystemConstants.LayerPath);

            if (layers != null)
            {
                groundLayers |= layers.GroundLayers;
            }
        }

        public void ApplyWaypoints()
        {
            var converter = new PedestrianWaypointsConverter();
            converter.ConvertWaypoints();
        }

        private void InternalWaypointClicked(PedestrianWaypointSettings clickedWaypoint, bool leftClick)
        {
            TriggerWaypointClickedEvent(clickedWaypoint, leftClick);
        }

        public void TriggerWaypointClickedEvent(WaypointSettingsBase clickedWaypoint, bool leftClick)
        {
            if (clickedWaypoint is PedestrianWaypointSettings pedestrianWaypoint)
            {
                PedestrianSettingsWindow.SetSelectedWaypoint(pedestrianWaypoint);
                OnWaypointClicked?.Invoke(pedestrianWaypoint, leftClick);
            }
        }

        public void ShowIntersectionWaypoints(Color waypointColor)
        {
            Drawer.ShowIntersectionWaypoints(waypointColor);
        }

        public void DrawPossibleDirectionWaypoints(List<WaypointSettingsBase> waypoints, Color waypointColor)
        {
            Drawer.DrawPossibleDirectionWaypoints(ConvertToPedestrianWaypoints(waypoints), waypointColor);
        }

        public List<WaypointSettingsBase> GetAllPedestrianWaypoints()
        {
            return new List<WaypointSettingsBase>(Data.GetAllWaypoints());
        }

        public int[] GetWaypointIndices(List<WaypointSettingsBase> selectedWaypoints)
        {
            return selectedWaypoints.ToListIndex(Data.GetAllWaypoints());
        }

        public static List<PedestrianWaypointSettings> ConvertToPedestrianWaypoints(List<WaypointSettingsBase> waypoints)
        {
            var result = new List<PedestrianWaypointSettings>(waypoints.Count);

            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] is PedestrianWaypointSettings pedestrianWaypoint)
                {
                    result.Add(pedestrianWaypoint);
                }
                else
                {
                    Debug.LogError(
                        $"Waypoint {waypoints[i].name} is not a PedestrianWaypointSettings",
                        waypoints[i]);
                }
            }

            return result;
        }

        public void Dispose()
        {
            if (_drawer != null)
            {
                _drawer.OnWaypointClicked -= InternalWaypointClicked;
                _drawer.OnDestroy();

            }
        }


    }
    [InitializeOnLoad]
    public static class PedestrianEditorBridgeInitializer
    {
        static PedestrianEditorBridgeInitializer()
        {
            Register();
            EditorApplication.playModeStateChanged += OnPlaymodeStateChanged;
        }

        private static void OnPlaymodeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                Register();
            }
        }

        static void Register()
        {
            PedestrianEditorBridgeRegistry.Unregister();
            PedestrianEditorBridgeRegistry.Register(new PedestrianEditorBridge());
        }
    }
}
