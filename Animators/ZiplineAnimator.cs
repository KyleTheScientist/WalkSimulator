using GorillaLocomotion;
using GorillaLocomotion.Gameplay;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator.Animators
{
    /// <summary>
    /// Riding ziplines. While walking, hold either mouse button near a zipline cable to grab it.
    /// You hang below the cable and slide along it with gravity (using the zipline's own speed
    /// settings), with both hands on the cable. Let go of the mouse button to drop off with your
    /// momentum, or press Space to jump off. You're released at the end of the line.
    /// The game's own zipline grabbing needs real controller positions and grip, so this rides the
    /// zipline's spline directly.
    /// </summary>
    public class ZiplineAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float GrabReach = .9f;            // cable within this distance of the grab point
        const float HangDistance = .75f;        // body hangs this far below the cable
        const float HandSpacing = .17f;         // each hand this far along the cable from center
        const float GripDrop = .07f;            // hands sit this far below the cable so fingers wrap over it
        const float Drag = .1f;                 // fraction of speed lost per second
        const float DefaultMaxSpeed = 12f;      // if the zipline has no max speed set
        const float JumpOffMultiplier = 1.3f;   // jumping off launches you this much harder than a normal jump
        const float RegrabDelay = .4f;          // seconds before you can grab again after letting go

        static readonly AccessTools.FieldRef<GorillaZipline, BezierSpline> splineRef =
            AccessTools.FieldRefAccess<GorillaZipline, BezierSpline>("spline");
        static readonly AccessTools.FieldRef<GorillaZipline, GorillaZiplineSettings> settingsRef =
            AccessTools.FieldRefAccess<GorillaZipline, GorillaZiplineSettings>("settings");

        public AnimatorBase returnTo;
        GorillaZipline zipline;
        BezierSpline spline;
        float t, speed;

        static GorillaZipline[] ziplines = new GorillaZipline[0];
        static float lastZiplineSearch = -100, lastRelease = -100;

        static float Scale => GTPlayer.Instance.scale;
        static bool GrabHeld => Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;

        /// <summary>Called while walking; grabs a nearby zipline while a mouse button is held.</summary>
        public static void CheckGrab()
        {
            var plugin = Plugin.Instance;
            if (!plugin.IsWalking || !GrabHeld || Time.time - lastRelease < RegrabDelay) return;

            if (Time.time - lastZiplineSearch > 5)
            {
                ziplines = FindObjectsByType<GorillaZipline>(FindObjectsSortMode.None);
                lastZiplineSearch = Time.time;
            }
            if (ziplines.Length == 0) return;

            // Reach up from the head
            Vector3 grabPoint = GTPlayer.Instance.headCollider.transform.position + Vector3.up * .3f * Scale;
            float reach = GrabReach * Scale;
            foreach (var candidate in ziplines)
            {
                if (!candidate || !candidate.isActiveAndEnabled) continue;
                var candidateSpline = splineRef(candidate);
                if (!candidateSpline) continue;
                float candidateT = ClosestT(candidateSpline, grabPoint);
                if (candidateSpline.GetPoint(candidateT).Distance(grabPoint) < reach)
                {
                    var ride = plugin.ziplineAnimator as ZiplineAnimator;
                    ride.returnTo = Rig.Instance.Animator;
                    ride.zipline = candidate;
                    ride.spline = candidateSpline;
                    ride.t = candidateT;
                    Rig.Instance.Animator = ride;
                    return;
                }
            }
        }

        // Closest point on the spline: coarse samples, then narrow in around the best one
        static float ClosestT(BezierSpline spline, Vector3 point)
        {
            const int samples = 64;
            float bestT = 0, bestDistance = float.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                float sampleT = i / (float)samples;
                float distance = (spline.GetPoint(sampleT) - point).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; bestT = sampleT; }
            }
            float step = 1f / samples;
            for (int i = 0; i < 12; i++)
            {
                step /= 2;
                foreach (float candidate in new[] { bestT - step, bestT + step })
                {
                    float clamped = Mathf.Clamp01(candidate);
                    float distance = (spline.GetPoint(clamped) - point).sqrMagnitude;
                    if (distance < bestDistance) { bestDistance = distance; bestT = clamped; }
                }
            }
            return bestT;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
            // Carry your momentum onto the cable
            speed = spline && rigidbody ? Vector3.Dot(rigidbody.linearVelocity, spline.GetDirection(t)) : 0;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            leftHand.followRate = rightHand.followRate = .1f;
            lastRelease = Time.time;
        }

        // The same speed a normal jump's impulse gives
        float JumpSpeed => 250 * Scale / rigidbody.mass;

        void Release(bool jump = false)
        {
            Vector3 velocity = rigidbody.linearVelocity;
            // Jumping off cancels any downward speed, then launches upward
            if (jump)
                velocity.y = Mathf.Max(velocity.y, 0) + JumpSpeed * JumpOffMultiplier;
            Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;
            rigidbody.linearVelocity = velocity;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null) return;
            if (!zipline || !spline || !GrabHeld)
                Release();
            else if (Keyboard.current.spaceKey.wasPressedThisFrame)
                Release(jump: true);
        }

        public override void Animate()
        {
            if (rig == null || !spline) return;
            float dt = Time.deltaTime;
            float s = Scale;

            // Slide: gravity along the cable, a little drag, capped at the zipline's max speed
            Vector3 direction = spline.GetDirection(t);
            var settings = settingsRef(zipline);
            float gravityMulti = settings ? settings.gravityMulti : 1;
            float maxSpeed = settings && settings.maxSpeed > 0 ? settings.maxSpeed : DefaultMaxSpeed;
            speed += Vector3.Dot(Physics.gravity, direction) * gravityMulti * dt;
            speed -= speed * Drag * dt;
            speed = Mathf.Clamp(speed, -maxSpeed * s, maxSpeed * s);

            // Advance along the spline by distance (GetVelocity is the spline's length per unit t)
            float metersPerT = Mathf.Max(spline.GetVelocity(t).magnitude, .001f);
            t += speed * dt / metersPerT;
            if (t <= 0 || t >= 1)
            {
                t = Mathf.Clamp01(t);
                Release();
                return;
            }

            // Hang below the cable; move the body directly so it can't lag behind
            Vector3 cable = spline.GetPoint(t);
            Vector3 hang = cable + Vector3.down * HangDistance * s;
            rig.active = false;
            rigidbody.linearVelocity = spline.GetDirection(t) * speed + (hang - body.position) * 10f;

            // Both hands hanging from the cable like a bar: just below it, palms toward it, fingers
            // pointing up and over the top
            Vector3 across = Vector3.Cross(Vector3.up, direction).normalized;
            if (across == Vector3.zero) across = body.right;
            // Palms face the way the body faces (whichever side of the cable that is)
            Vector3 palmDirection = Vector3.Dot(across, body.forward) >= 0 ? across : -across;
            // Each hand on its own side of the body along the cable
            float sideSign = Vector3.Dot(direction, body.right) >= 0 ? 1 : -1;
            // When the cable runs front-to-back along the body, one hand is behind the other; turn
            // that one around so the palms face each other like a real grip
            bool alongBody = Mathf.Abs(Vector3.Dot(direction, body.forward)) > .5f;
            float handT = HandSpacing * s / metersPerT;
            foreach (var hand in new[] { leftHand, rightHand })
            {
                float offset = (hand.isLeft ? -handT : handT) * sideSign;
                Vector3 grip = spline.GetPoint(Mathf.Clamp01(t + offset));
                bool rear = alongBody && Vector3.Dot(grip - body.position, body.forward) < Vector3.Dot(cable - body.position, body.forward);
                Vector3 palm = rear ? -palmDirection : palmDirection;
                Vector3 position = grip + (Vector3.down * GripDrop - palm * .03f) * s;
                hand.followInBodySpace = true;
                hand.followRate = .5f;
                hand.Place(position, -palm, Vector3.up);
            }
        }
    }
}
