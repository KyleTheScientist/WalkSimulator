using GorillaLocomotion;
using GorillaLocomotion.Gameplay;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator.Animators
{
    /// <summary>
    /// Swinging on ropes. While walking, hold either mouse button near a rope to grab it.
    /// Uses the game's own rope attach/detach, so the rope simulation takes your weight and other
    /// players see you on it. WASD pumps the swing, Space/Ctrl climb up/down the rope, and letting
    /// go of the mouse button flings you off with the rope's momentum.
    /// </summary>
    public class RopeAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float GrabReach = .8f;            // rope bone within this distance of the grab point
        const float HangDistance = .55f;        // body hangs this far below the grabbing hand
        const float PumpAcceleration = 6f;      // m/s^2 the held direction adds to the rope's swing
        const float ClimbRate = 4f;             // rope bones per second when climbing
        const float ReleaseBoost = 1.2f;        // multiplier on the rope's velocity when letting go
        const float RegrabDelay = .4f;          // seconds before you can grab again after letting go

        public AnimatorBase returnTo;
        GorillaRopeSwing rope;
        float boneIndex;                        // fractional so climbing is smooth
        int upStep;                             // which way along the bones is up the rope (-1 or 1)
        Vector3 lastBonePosition, boneVelocity;
        bool attached;

        static GorillaRopeSwing[] ropes = new GorillaRopeSwing[0];
        static float lastRopeSearch = -100, lastRelease = -100;

        static float Scale => GTPlayer.Instance.scale;
        static bool GrabHeld => Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;
        int Bone => Mathf.Clamp(Mathf.RoundToInt(boneIndex), 0, rope.nodes.Length - 1);
        Transform BoneTransform => rope.nodes[Bone];

        /// <summary>Called while walking; grabs a nearby rope while a mouse button is held.</summary>
        public static void CheckGrab()
        {
            var plugin = Plugin.Instance;
            if (!plugin.IsWalking || !GrabHeld || Time.time - lastRelease < RegrabDelay) return;

            if (Time.time - lastRopeSearch > 5)
            {
                ropes = FindObjectsByType<GorillaRopeSwing>(FindObjectsSortMode.None);
                lastRopeSearch = Time.time;
            }
            if (ropes.Length == 0) return;

            Vector3 grabPoint = GTPlayer.Instance.headCollider.transform.position + Vector3.up * .2f * Scale;
            float bestDistance = GrabReach * Scale;
            GorillaRopeSwing bestRope = null;
            int bestBone = 0;
            foreach (var candidate in ropes)
            {
                if (!candidate || !candidate.isActiveAndEnabled || candidate.nodes == null) continue;
                for (int i = 0; i < candidate.nodes.Length; i++)
                {
                    if (!candidate.nodes[i]) continue;
                    float distance = candidate.nodes[i].position.Distance(grabPoint);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestRope = candidate;
                        bestBone = i;
                    }
                }
            }
            if (!bestRope) return;

            var swing = plugin.ropeAnimator as RopeAnimator;
            swing.returnTo = Rig.Instance.Animator;
            swing.rope = bestRope;
            swing.boneIndex = bestBone;
            var nodes = bestRope.nodes;
            swing.upStep = nodes[0].position.y >= nodes[nodes.Length - 1].position.y ? -1 : 1;
            Rig.Instance.Animator = swing;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
            if (!rope) return;
            lastBonePosition = BoneTransform.position;
            boneVelocity = Vector3.zero;
            try
            {
                // Hand the rope our velocity so grabbing it on the run starts it swinging
                rope.AttachLocalPlayer(XRNode.RightHand, BoneTransform, Vector3.zero, rigidbody.linearVelocity);
                attached = true;
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        public override void Cleanup()
        {
            base.Cleanup();
            Detach();
            leftHand.followRate = rightHand.followRate = .1f;
            lastRelease = Time.time;
        }

        void Detach()
        {
            if (!attached) return;
            attached = false;
            try { if (rope) rope.DetachLocalPlayer(); }
            catch (Exception e) { Logging.Exception(e); }
        }

        void Release()
        {
            Vector3 velocity = boneVelocity * ReleaseBoost;
            Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;
            rigidbody.linearVelocity = velocity;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null) return;
            if (!rope || !rope.isActiveAndEnabled || !GrabHeld)
                Release();
        }

        public override void Animate()
        {
            if (rig == null || !rope) return;
            float dt = Time.deltaTime;
            float s = Scale;
            Vector3 input = InputHandler.inputDirection;

            // Climb up or down the rope
            int previousBone = Bone;
            boneIndex = Mathf.Clamp(boneIndex + upStep * input.y * ClimbRate * dt, 0, rope.nodes.Length - 1);
            if (Bone != previousBone && attached)
            {
                // Re-attach at the new bone so the rope (and other players) see where you're holding
                try
                {
                    rope.DetachLocalPlayer();
                    rope.AttachLocalPlayer(XRNode.RightHand, BoneTransform, Vector3.zero, boneVelocity);
                }
                catch (Exception e) { Logging.Exception(e); }
            }

            // Track how the grabbed point is moving
            Vector3 bonePosition = BoneTransform.position;
            boneVelocity = (bonePosition - lastBonePosition) / Mathf.Max(dt, .0001f);
            lastBonePosition = bonePosition;

            // Pump the swing in the held direction
            Vector3 push = body.TransformDirection(new Vector3(input.x, 0, input.z));
            push.y = 0;
            if (push != Vector3.zero)
            {
                try
                {
                    rope.SetVelocity(Bone, boneVelocity + push.normalized * PumpAcceleration * s * dt, true, default);
                }
                catch (Exception e) { Logging.Exception(e); }
            }

            // Hang below the hand; drive the body directly so it stays with the rope
            Vector3 hang = bonePosition + Vector3.down * HangDistance * s;
            rig.active = false;
            rigidbody.linearVelocity = boneVelocity + (hang - body.position) * 10f;

            // Right hand on the grabbed bone, left hand one bone higher; knuckles facing out
            int upperBone = Mathf.Clamp(Bone + upStep, 0, rope.nodes.Length - 1);
            Vector3 ropeUp = (rope.nodes[upperBone].position - bonePosition);
            if (ropeUp.sqrMagnitude < .0001f) ropeUp = Vector3.up;
            Vector3 lower = bonePosition, upper = Bone == upperBone ? bonePosition + ropeUp.normalized * .12f * s : rope.nodes[upperBone].position;
            foreach (var hand in new[] { leftHand, rightHand })
            {
                Vector3 grip = hand.isLeft ? upper : lower;
                hand.followInBodySpace = true;
                hand.followRate = .5f;
                // Wrap around the rope: back of the hand faces away from the body
                Vector3 outward = Vector3.ProjectOnPlane(grip - body.position, ropeUp).normalized;
                if (outward == Vector3.zero) outward = body.forward;
                hand.Place(grip, outward, ropeUp);
            }
        }
    }
}
