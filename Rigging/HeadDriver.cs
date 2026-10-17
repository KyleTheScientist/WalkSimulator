using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine;
using Thirpy = Unity.Cinemachine.CinemachineThirdPersonFollow;
using GorillaLocomotion;

namespace WalkSimulator.Rigging
{
    public class HeadDriver : MonoBehaviour
    {
        public static HeadDriver Instance;
        public Transform thirpyTarget, head;
        public GameObject cameraObject;
        Vector3 offset = new Vector3(0, 0f, 0);
        Camera overrideCam;

        public bool turn = true;
        bool _lockCursor;
        public bool LockCursor
        {
            get
            {
                return _lockCursor;
            }
            set
            {
                _lockCursor = value;
                if (_lockCursor)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

        void Awake()
        {
            Instance = this;

        }
        void Start()
        {
            var thirpy = FindAnyObjectByType<Thirpy>();
            thirpyTarget = thirpy.FollowTarget;
            var shoulderCamera = WalkSimulator.Tools.Extensions.ShoulderCamera();
            //cosmeticsTransform = GorillaTagger.Instance.mainCamera.transform.Find("Cosmetics");

            cameraObject = Plugin.Instance.bundle.LoadAsset<GameObject>("Override Camera");
            cameraObject = Instantiate(cameraObject);
            overrideCam = cameraObject.GetComponent<Camera>();
            overrideCam.nearClipPlane = shoulderCamera.nearClipPlane;
            overrideCam.farClipPlane = shoulderCamera.farClipPlane;
            overrideCam.cullingMask = shoulderCamera.cullingMask;
            overrideCam.depth = shoulderCamera.depth + 1;
            overrideCam.targetDisplay = shoulderCamera.targetDisplay;  
            overrideCam.fieldOfView = 90;
            overrideCam.enabled = false;
        }

        void LateUpdate()
        {
            cameraObject.transform.position = GTPlayer.Instance.headCollider.transform.TransformPoint(offset);
            cameraObject.transform.forward = head.forward;

            // Turn the camera with the mouse
            if (turn)
            {
                // Horizontal Axis
                GTPlayer.Instance.Turn(Mouse.current.delta.value.x / 10f);

                //Vertical Axis
                var angles = GorillaTagger.Instance.offlineVRRig.headConstraint.eulerAngles;
                angles.x = angles.x - Mouse.current.delta.value.y / 10f;
                if (angles.x > 180)
                    angles.x -= 360;
                //clamp rotation between -45 and 45 
                angles.x = Mathf.Clamp(angles.x, -60, 60);
                GorillaTagger.Instance.offlineVRRig.headConstraint.eulerAngles = angles;
                angles.y += 90;
                //head.localEulerAngles = angles;
                //Camera.main.transform.localEulerAngles = angles;
                thirpyTarget.localEulerAngles = new Vector3(angles.x, 0, 0);
                GTPlayer.Instance.headCollider.transform.localEulerAngles = new Vector3(angles.x, 0, 0);
            }
        }

        void OverrideHeadMovement()
        {
            //GameObject replacement = new GameObject("WalkSim Replacement Head");
            head = GorillaTagger.Instance.offlineVRRig.head.rigTarget;
            //GorillaTagger.Instance.offlineVRRig.head.rigTarget = replacement.transform;
        }

        internal void ToggleCam()
        {
            overrideCam.enabled = !overrideCam.enabled;
        }

        public bool FirstPerson
        {
            get
            {
                return overrideCam.enabled;
            }
            set
            {
                overrideCam.enabled = value;
            }
        }

        void OnEnable()
        {
            Logging.Debug("Enabled");
            if (Rig.Instance.Animator is null) return;
            LockCursor = true;
            OverrideHeadMovement();
        }

        void OnDisable()
        {
            if (!head) return;
            LockCursor = false;
            GorillaTagger.Instance.offlineVRRig.head.rigTarget = head;
        }
    }
}
