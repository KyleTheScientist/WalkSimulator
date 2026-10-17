using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine;
using Thirpy = Unity.Cinemachine.Cinemachine3rdPersonFollow;
using GorillaLocomotion;
using WalkSimulator.Tools;

namespace WalkSimulator.Rigging
{
    public class HeadDriver : MonoBehaviour
    {
        public static HeadDriver Instance;
        public Transform thirpyTarget, head;
        Vector3 offset = new Vector3(0, 0f, 0);

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
            thirpyTarget = FindFollowTarget();
        }

        // The transform the shoulder camera follows; tilting it is what makes the monitor view look up/down
        Transform FindFollowTarget()
        {
            var thirpy = FindAnyObjectByType<Thirpy>(FindObjectsInactive.Include);
            if (thirpy && thirpy.FollowTarget)
                return thirpy.FollowTarget;
            var follow = FindAnyObjectByType<GorillaCameraFollow>(FindObjectsInactive.Include);
            if (follow && follow.cinemachineCamera)
                return follow.cinemachineCamera.Follow;
            Logging.Warning("Couldn't find the shoulder camera's follow target; vertical look won't move the monitor view");
            return null;
        }

        void LateUpdate()
        {
            if (!head) return;

            // First person: look through the game's own camera from the head
            if (firstPerson)
            {
                ShoulderCameraControl.Take(this, FirstPersonFieldOfView);
                ShoulderCameraControl.Place(GTPlayer.Instance.headCollider.transform.TransformPoint(offset), Quaternion.LookRotation(head.forward));
            }
            else
                ShoulderCameraControl.Release(this);

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
                // Look up to 80 degrees up and 85 down (short of straight up/down, where the view flips),
                // so buttons low and close are reachable
                angles.x = Mathf.Clamp(angles.x, -80, 85);
                GorillaTagger.Instance.offlineVRRig.headConstraint.eulerAngles = angles;
                angles.y += 90;
                //head.localEulerAngles = angles;
                //Camera.main.transform.localEulerAngles = angles;
                if (thirpyTarget)
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

        bool firstPerson;
        const float FirstPersonFieldOfView = 90;

        internal void ToggleCam()
        {
            firstPerson = !firstPerson;
        }

        public bool FirstPerson
        {
            get { return firstPerson; }
            set { firstPerson = value; }
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
            ShoulderCameraControl.Release(this);
            if (!head) return;
            LockCursor = false;
            GorillaTagger.Instance.offlineVRRig.head.rigTarget = head;
        }
    }
}
