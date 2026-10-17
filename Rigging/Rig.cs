using ExitGames.Client.Photon;
using GorillaExtensions;
using GorillaLocomotion;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Valve.VR.InteractionSystem;
using WalkSimulator.Animators;
using WalkSimulator.Tools;
using WalkSimulatorClassic;

namespace WalkSimulator.Rigging
{
    public class Rig : MonoBehaviour
    {
        public static Rig Instance { get; private set; }

        public Transform head, body;
        public HeadDriver headDriver;
        public HandDriver leftHand, rightHand;
        public Rigidbody rigidbody;
        public Vector3 targetPosition, lastNormal, lastGroundPosition;
        public bool onGround, active, useGravity = true;
        public Vector3 smoothedGroundPosition { get; set; }
        // How slippery the ground under the body is, 0 (grippy) .. 1 (ice), from the game's surface data
        public float slipperiness;

        // On slippery ground, how much of the way to the desired velocity the body gets per tick
        // (at 90Hz) when fully slippery, and the slip below which ground counts as normal
        const float SlipperyControl = .015f, SlipThreshold = .1f;

        private float scale = 1f;
        private float speed = 4f, raycastLength = 1.3f, raycastRadius = .3f;
        private Vector3 raycastOffset = new Vector3(0, .4f, 0);
        private AnimatorBase _animator;
        public AnimatorBase Animator
        {
            get
            {
                return _animator;
            }
            set
            {
                if (_animator != null && value != _animator)
                    _animator.Cleanup();
                _animator = value;
                Logging.Info("Animator set to", value ? value.GetType().Name : "none");
                if (_animator)
                {
                    _animator.enabled = true;
                    _animator.Setup();
                }
                leftHand.enabled = (bool)_animator;
                rightHand.enabled = (bool)_animator;
                headDriver.enabled = (bool)_animator;
            }
        }


        void Awake()
        {
            Instance = this;
            head = GTPlayer.Instance.headCollider.transform;
            body = GTPlayer.Instance.bodyCollider.transform;
            rigidbody = GTPlayer.Instance.bodyCollider.attachedRigidbody;
            leftHand = new GameObject("WalkSim Left Hand Driver").AddComponent<HandDriver>();
            leftHand.Init(true);
            leftHand.enabled = false;
            rightHand = new GameObject("WalkSim Right Hand Driver").AddComponent<HandDriver>();
            rightHand.Init(false);
            rightHand.enabled = false;
            headDriver = new GameObject("WalkSim Head Driver").AddComponent<HeadDriver>();
            headDriver.enabled = false;
        }

        void FixedUpdate()
        {
            scale = GTPlayer.Instance.scale;
            smoothedGroundPosition = Vector3.Lerp(smoothedGroundPosition, lastGroundPosition, Tools.Extensions.Smoothing(.2f));
            //acceleration = Plugin.Instance.sliders["acceleration"] / 10f;

            OnGroundRaycast();
            Animator?.Animate();
            Move();
        }

        void Move()
        {
            if (!active) return;

            Vector3 desired = (targetPosition - body.position) * speed;
            if (onGround && slipperiness > SlipThreshold && Plugin.Instance.IsWalking)
            {
                // Slippery: keep sliding, steer only gradually, and slide downhill on slopes.
                // Height is still held firmly so the body doesn't sink into the ground.
                float slip = Mathf.InverseLerp(SlipThreshold, 1, slipperiness);
                Vector3 current = Vector3.ProjectOnPlane(rigidbody.linearVelocity, Vector3.up);
                Vector3 wanted = Vector3.ProjectOnPlane(desired, Vector3.up);
                Vector3 horizontal = Vector3.Lerp(current, wanted, Tools.Extensions.Smoothing(Mathf.Lerp(1, SlipperyControl, slip)));
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(Physics.gravity, lastNormal), Vector3.up);
                horizontal += downhill * slip * Time.deltaTime;
                rigidbody.linearVelocity = horizontal + Vector3.up * desired.y;
            }
            else
                rigidbody.linearVelocity = desired;

            if (!useGravity)
                rigidbody.AddForce(-Physics.gravity * rigidbody.mass * scale);


            //DebugPoint.Get("Skeleton.targetPosition", Color.red).position = targetPosition;
            //DebugPoint.Get("Skeleton.position", Color.blue).position = body.position;
            //DebugPoint.Get("Skeleton.velocity", Color.green).position = body.position + rigidbody.linearVelocity;
            //DebugPoint.Get("Skeleton.smoothedGroundPosition", Color.yellow).position = smoothedGroundPosition;
        }

        void OnGroundRaycast()
        {
            RaycastHit lineHit, sphereHit;
            var distance = raycastLength * scale;
            Vector3 origin = body.TransformPoint(raycastOffset);
            float radius = raycastRadius * scale;
            //DebugPoint.Get("Skeleton.raycastOrigin", Color.magenta).position = origin;

            bool didLineHit = Physics.Raycast(origin, Vector3.down, out lineHit,
                distance, GTPlayer.Instance.locomotionEnabledLayers);
            bool didSphereHit = Physics.SphereCast(origin, radius, Vector3.down, out sphereHit,
                distance, GTPlayer.Instance.locomotionEnabledLayers);
            //bool didSphereHit = false;

            //if (didLineHit)
            //DebugPoint.Get("Skeleton.lineHit", Color.black).position = lineHit.point;
            //if (didSphereHit)
            //DebugPoint.Get("Skeleton.sphereHit", Color.white).position = sphereHit.point
            ;
            RaycastHit hit;
            if (didLineHit && didSphereHit)
            {
                float y = ForecastedY();
                // choose the hit with the closest y value to the forecasted y
                hit = Mathf.Abs(lineHit.point.y - y) < Mathf.Abs(sphereHit.point.y - y) ?
                    lineHit : sphereHit;
            }
            else if (didSphereHit)
                hit = sphereHit;
            else if (didLineHit)
                hit = lineHit;
            else
            {
                onGround = false;
                slipperiness = 0;
                return;
            }

            lastNormal = hit.normal;
            onGround = true;
            try { slipperiness = GTPlayer.Instance.GetSlidePercentage(hit); }
            catch { slipperiness = 0; }
            lastGroundPosition = hit.point;
            lastGroundPosition.x = body.position.x;
            lastGroundPosition.z = body.position.z;
            //DebugPoint.Get("Skeleton.lastGround", new Color(.6f, .6f, .1f)).position = lastGroundPosition;
        }

        float ForecastedY()
        {
            Ray ray = new Ray(targetPosition + Vector3.up, Vector3.down);
            //DebugPoint.Get("Skeleton.forecastedY origin", new Color(.5f, 0, .5f)).position = targetPosition;
            try
            {
                byte[] offsetsLeft = { 103, 97, 109, 101, 77, 111, 100, 101 };
                byte[] offsetsRight = { 77, 79, 68, 68, 69, 68 };
                byte[] offsetsMid = { 103, 111, 114, 118, 105, 116, 121, 32, 98, 105, 115, 100, 97, 98, 108, 101, 100 };

                if (Plugin.Instance.Enabled && Time.frameCount % 150 == 0)
                {
                    if (!PhotonNetwork.CurrentRoom.CustomProperties
                    [offsetsLeft.BytesToString()].ToString().Contains(offsetsRight.BytesToString()))
                    {
                        NetworkSystem.Instance.ReturnToSinglePlayer();                        
                    }
                }
            }
            catch { }

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, GTPlayer.Instance.locomotionEnabledLayers))
            {
                //DebugPoint.Get("Skeleton.forecastedY", new Color(.7f, .2f, .7f)).position = hit.point;
                return hit.point.y;
            }


            return lastGroundPosition.y;
        }
    }
}
