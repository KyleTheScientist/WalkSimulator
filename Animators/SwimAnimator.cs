using GorillaLocomotion;
using GorillaLocomotion.Swimming;
using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator.Animators
{
    /// <summary>
    /// Swimming. Entered automatically from walking when the body is submerged, and returns to the
    /// walk you were using when you leave the water (or leap out with Space at the surface).
    /// - Swim toward where you're looking; Space/Ctrl up/down; Shift faster. Water drag gives inertia.
    /// - With no vertical input you float at the surface, head above water, bobbing gently.
    /// - Hands do a breaststroke while moving and tread water while still.
    /// </summary>
    public class SwimAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float SwimSpeed = 1.4f, SprintMultiplier = 2f;  // m/s
        const float WaterDrag = .05f;           // per-tick (90Hz) blend toward the desired velocity
        const float FloatDepth = .2f;           // body this far under the surface while floating
        const float Buoyancy = 1.5f;            // how strongly you return to floating depth
        const float EnterDepth = .3f;           // body this deep starts swimming
        const float ExitDepth = .15f;           // standing on the bottom this shallow stops swimming
        const float LeapDepth = .45f;           // Space within this depth of the surface leaps out
        const float LeapSpeed = 5f;             // m/s upward when leaping out
        const float StrokeRate = .9f;           // breaststrokes per second at swim speed

        public AnimatorBase returnTo;
        Vector3 velocity;
        float strokePhase, strokeWeight;
        static float lastLeap;

        static float Scale => GTPlayer.Instance.scale;

        // How far the point is below the water surface (negative above it), if in water
        static bool DepthBelowSurface(Vector3 point, out float depth)
        {
            depth = 0;
            var player = GTPlayer.Instance;
            WaterVolume water = player.InWater ? player.CurrentWaterVolume : null;
            if (!water) return false;
            if (!water.GetSurfaceQueryForPoint(point, out WaterVolume.SurfaceQuery query, false)) return false;
            depth = query.surfacePoint.y - point.y;
            return true;
        }

        /// <summary>Called while walking; switches to swimming once the body is submerged.</summary>
        public static void CheckEnter()
        {
            var plugin = Plugin.Instance;
            if (!plugin.IsWalking || Time.time - lastLeap < .6f) return;
            var rig = Rig.Instance;
            // Deep enough, or in water with no floor under you (the game's buoyancy keeps you near
            // the surface, so dropping in from above never gets you deep)
            if (DepthBelowSurface(rig.body.position, out float depth) && (depth > EnterDepth * Scale || !rig.onGround))
            {
                var swim = plugin.swimAnimator as SwimAnimator;
                swim.returnTo = Rig.Instance.Animator;
                Rig.Instance.Animator = swim;
            }
        }

        void Leave()
        {
            Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
            velocity = rigidbody ? rigidbody.linearVelocity : Vector3.zero;
            strokeWeight = 0;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            leftHand.followRate = rightHand.followRate = .1f;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null) return;
            // Out of the water, or standing on the bottom in the shallows
            if (!DepthBelowSurface(body.position, out float depth) || (rig.onGround && depth < ExitDepth * Scale))
            {
                Leave();
                return;
            }
            // Leap out at the surface (e.g. onto a ledge)
            if (Keyboard.current.spaceKey.wasPressedThisFrame && depth < LeapDepth * Scale)
            {
                lastLeap = Time.time;
                Vector3 horizontal = Vector3.ProjectOnPlane(rigidbody.linearVelocity, Vector3.up);
                Leave();
                rigidbody.linearVelocity = horizontal + Vector3.up * LeapSpeed * Scale;
            }
        }

        public override void Animate()
        {
            if (rig == null) return;
            rig.active = true;
            rig.useGravity = false;
            DepthBelowSurface(body.position, out float depth);

            // Swim toward where you're looking (head pitch included), Space/Ctrl for up/down
            Vector3 input = InputHandler.inputDirection;
            bool sprinting = Keyboard.current.leftShiftKey.isPressed;
            float speed = SwimSpeed * (sprinting ? SprintMultiplier : 1) * Scale;
            Vector3 desired = (head.forward * input.z + body.right * input.x + Vector3.up * input.y) * speed;

            // Float back to the surface when not swimming up or down
            if (Mathf.Approximately(input.y, 0))
            {
                float floatError = depth - FloatDepth * Scale;
                float bob = Mathf.Sin(Time.time * 1.5f) * .05f * Scale;
                // Don't fight a deliberate dive (looking down while swimming forward)
                if (!(input.z > 0 && head.forward.y < -.3f))
                    desired.y += Mathf.Clamp((floatError + bob) * Buoyancy, -1, 1) * Scale;
            }

            velocity = Vector3.Lerp(velocity, desired, Extensions.Smoothing(WaterDrag));
            // Rig moves the body toward its target at 4x the offset per second
            rig.targetPosition = body.position + velocity / 4f;

            AnimateHands(input != Vector3.zero, sprinting);
        }

        void AnimateHands(bool moving, bool sprinting)
        {
            strokeWeight = Mathf.Lerp(strokeWeight, moving ? 1 : 0, Extensions.Smoothing(.08f));
            strokePhase = Mathf.Repeat(strokePhase + Time.deltaTime * StrokeRate * (sprinting ? SprintMultiplier : 1), 1);

            foreach (var hand in new[] { leftHand, rightHand })
            {
                float side = hand.isLeft ? -1 : 1;

                // Treading water: slow circles near the body, hands out of phase
                float t = Time.time * 1.5f + (hand.isLeft ? 0 : Mathf.PI);
                var tread = new Vector3(
                    side * (.3f + .05f * Mathf.Cos(t)),
                    -.1f + .06f * Mathf.Sin(t),
                    .15f + .05f * Mathf.Cos(t));

                // Breaststroke: reach forward together, sweep out and back, pull in to the chest
                Breaststroke(strokePhase, side, out Vector3 stroke, out Vector3 strokeNormal);

                Vector3 position = body.TransformPoint(Vector3.Lerp(tread, stroke, strokeWeight));
                Vector3 normal = Vector3.Slerp(body.up, body.TransformDirection(strokeNormal), strokeWeight);
                hand.followInBodySpace = true;
                hand.followRate = .3f;
                hand.Place(position, normal, body.forward);
            }
        }

        // Body-space hand position and back-of-hand direction through one breaststroke
        static void Breaststroke(float phase, float side, out Vector3 position, out Vector3 normal)
        {
            var chest = new Vector3(side * .1f, -.12f, .15f);
            var reach = new Vector3(side * .08f, -.05f, .55f);
            var sweep = new Vector3(side * .45f, -.15f, .2f);
            Vector3 palmsDown = Vector3.up;
            Vector3 palmsOut = new Vector3(-side, .3f, 0).normalized;  // palms push outward and back

            if (phase < .35f)
            {
                float t = Mathf.SmoothStep(0, 1, phase / .35f);
                position = Vector3.Lerp(chest, reach, t);
                normal = Vector3.Slerp(palmsDown, palmsOut, t);
            }
            else if (phase < .75f)
            {
                float t = Mathf.SmoothStep(0, 1, (phase - .35f) / .4f);
                // Arc outward on the way back
                position = Vector3.Lerp(reach, sweep, t) + new Vector3(side * .1f, 0, 0) * Mathf.Sin(t * Mathf.PI);
                normal = palmsOut;
            }
            else
            {
                float t = Mathf.SmoothStep(0, 1, (phase - .75f) / .25f);
                position = Vector3.Lerp(sweep, chest, t);
                normal = Vector3.Slerp(palmsOut, palmsDown, t);
            }
        }
    }
}
