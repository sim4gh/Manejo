using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset.Demo
{
    public class SimpleCameraController : MonoBehaviour
    {
        public bool flipForward;
        public float backupRotationThreshold = 0.2f;
        public Rigidbody carRigidbody;
        public Transform mainTarget;
        public Transform[] cameraViews;

        public Component vCamMain;
        public Component vCamView;

        public KeyCode lookBackKey = KeyCode.B;
        public KeyCode changeCamKey = KeyCode.C;


        private Quaternion _originalMainTargetRot;
        private Quaternion[] _originalCameraViewsRot;

        private void Start()
        {
            _originalMainTargetRot = mainTarget.localRotation;
            _originalCameraViewsRot = new Quaternion[cameraViews.Length];
            for (int i = 0; i < cameraViews.Length; ++i)
            {
                if (cameraViews[i] == null)
                {
                    continue;
                }
                _originalCameraViewsRot[i] = cameraViews[i].transform.rotation;
            }
            _viewsCount = cameraViews.Length + 1;
        }

        private int _viewsCount;
        private int _currentView = 0;
        bool _isLookingBack = false;

        private void SetComponentActive(Component cam, bool active)
        {
            if (cam != null)
                cam.gameObject.SetActive(active);
        }

        private void SetFollow(Component cam, Transform target)
        {
            if (cam == null || target == null) return;

            var type = cam.GetType();
            if (!type.FullName.Contains("Cinemachine.CinemachineVirtualCamera")) return;

            var followProp = type.GetProperty("Follow");
            if (followProp != null)
                followProp.SetValue(cam, target);
        }

#if ENABLE_INPUT_SYSTEM

        public void OnLookBackKey(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            _isLookingBack = context.ReadValueAsButton();
            if (_isLookingBack)
            {
                mainTarget.localRotation = _originalMainTargetRot * Quaternion.Euler(0, 180f, 0);
            }
            else
            {
                mainTarget.localRotation = _originalMainTargetRot;
            }
        }

        public void OnChangeCameraKey(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _currentView = (_currentView + 1) % _viewsCount;
                if (_currentView == 0)
                {
                    SetComponentActive(vCamMain, true);
                    SetComponentActive(vCamView, false);
                }
                else
                {
                    SetComponentActive(vCamView, true);
                    SetComponentActive(vCamMain, false);
                    SetFollow(vCamView, cameraViews[_currentView - 1]);
                }
            }
        }

#endif

        void Update()
        {
            if (!_isLookingBack && carRigidbody)
            {
                float dotProduct = Vector3.Dot(carRigidbody.linearVelocity, (flipForward ? -1 : 1) * carRigidbody.transform.forward);
                if (dotProduct < -backupRotationThreshold)
                {
                    mainTarget.localRotation = _originalMainTargetRot * Quaternion.Euler(0, 180f, 0);
                }
                else if (dotProduct > backupRotationThreshold)
                {
                    mainTarget.localRotation = _originalMainTargetRot;
                }
            }
#if !ENABLE_INPUT_SYSTEM
            if (Input.GetKeyDown(lookBackKey))
            {
                _isLookingBack = true;
                mainTarget.localRotation = _originalMainTargetRot * Quaternion.Euler(0, 180f, 0);
            }
            else if (Input.GetKeyUp(lookBackKey))
            {
                _isLookingBack = false;
                mainTarget.localRotation = _originalMainTargetRot;
            }
            if (Input.GetKeyDown(changeCamKey))
            {
                _currentView = (_currentView + 1) % _viewsCount;
                if (_currentView == 0)
                {
                    SetComponentActive(vCamMain, true);
                    SetComponentActive(vCamView, false);
                }
                else
                {
                    SetComponentActive(vCamView, true);
                    SetComponentActive(vCamMain, false);
                    SetFollow(vCamView, cameraViews[_currentView - 1]);
                }
            }
#endif
        }
    }
}
