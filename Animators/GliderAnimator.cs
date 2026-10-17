using GorillaLocomotion;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator.Animators
{
    /// <summary>
    /// Hang gliders. While walking, hold either mouse button near a glider to grab it.
    /// Rather than faking flight, this lets the game's own glider do the flying: the real hand
    /// (controller) positions are moved onto the handle and grip is squeezed, so the game picks the
    /// glider up normally, and then the hands are held overhead and moved to steer, the way you would
    /// in VR. A/D tilts the hands to roll, W/S moves them forward/back. Let go of the mouse button to drop it.
    /// </summary>
    public class GliderAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float GrabReach = 1.3f;           // handle within this distance of the head
        const float GrabTimeout = .75f;         // give up if the game hasn't picked it up by then
        const float HandSpread = .3f;           // each hand this far to the side while flying
        const float HandHeight = .35f;          // hands this far above the head while flying
        const float HandForward = .15f;         // and this far in front
        const float RollHeight = .15f;          // A/D raises one hand and lowers the other this much
        const float PitchReach = .2f;           // W/S moves the hands this far forward/back
        const float RegrabDelay = .5f;

        static readonly AccessTools.FieldRef<GliderHoldable, InteractionPoint> handleRef =
            AccessTools.FieldRefAccess<GliderHoldable, InteractionPoint>("handle");

        public AnimatorBase returnTo;
        GliderHoldable glider;
        float grabStart;
        bool wasHeld;

        static GliderHoldable[] gliders = new GliderHoldable[0];
        static float lastSearch = -100, lastRelease = -100;

        static float Scale => GTPlayer.Instance.scale;
        static bool GrabHeld => Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;

        bool IsHeld
        {
            get
            {
                var interactor = EquipmentInteractor.instance;
                if (!interactor || !glider) return false;
                return ReferenceEquals(interactor.leftHandHeldEquipment, glider)
                    || ReferenceEquals(interactor.rightHandHeldEquipment, glider);
            }
        }

        /// <summary>Called while walking; grabs a nearby glider while a mouse button is held.</summary>
        public static void CheckGrab()
        {
            var plugin = Plugin.Instance;
            if (!plugin.IsWalking || !GrabHeld || Time.time - lastRelease < RegrabDelay) return;

            if (Time.time - lastSearch > 5)
            {
                gliders = FindObjectsByType<GliderHoldable>(FindObjectsSortMode.None);
                lastSearch = Time.time;
            }
            Vector3 head = GTPlayer.Instance.headCollider.transform.position;
            float bestDistance = GrabReach * Scale;
            GliderHoldable best = null;
            foreach (var candidate in gliders)
            {
                if (!candidate || !candidate.isActiveAndEnabled) continue;
                var handle = handleRef(candidate);
                Vector3 point = handle ? handle.transform.position : candidate.transform.position;
                float distance = point.Distance(head);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            if (!best) return;

            var animator = plugin.gliderAnimator as GliderAnimator;
            animator.returnTo = Rig.Instance.Animator;
            animator.glider = best;
            Rig.Instance.Animator = animator;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
            grabStart = Time.time;
            wasHeld = false;
        }

        public override void Cleanup()
        {
            // base.Cleanup resets the hands: grip off (which lets go) and controllers hidden again
            base.Cleanup();
            leftHand.followRate = rightHand.followRate = .1f;
            lastRelease = Time.time;
            try
            {
                if (IsHeld)
                    EquipmentInteractor.instance.ForceDropEquipment(glider);
            }
            catch (System.Exception e) { Logging.Exception(e); }
        }

        void Leave()
        {
            Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null) return;
            if (!glider || !GrabHeld)
                Leave();
        }

        public override void Animate()
        {
            if (rig == null || !glider) return;
            float s = Scale;
            bool held = IsHeld;
            if (held && !wasHeld)
                Logging.Info("Grabbed glider");
            if (!held && wasHeld)
            {
                // The game let go of it (respawned, taken by someone, ...)
                Leave();
                return;
            }
            wasHeld = held;

            foreach (var hand in new[] { leftHand, rightHand })
            {
                // Real hand positions matter here: the game grabs and steers with them
                hand.hideControllerTransform = false;
                hand.grip = true;
                hand.followInBodySpace = true;
                hand.followRate = .5f;
            }

            if (!held)
            {
                // Reach for the handle and squeeze until the game picks it up
                if (Time.time - grabStart > GrabTimeout)
                {
                    Logging.Info("Couldn't grab the glider");
                    Leave();
                    return;
                }
                var handle = handleRef(glider);
                Transform handleTransform = handle ? handle.transform : glider.transform;
                foreach (var hand in new[] { leftHand, rightHand })
                {
                    float side = hand.isLeft ? -1 : 1;
                    Vector3 point = handleTransform.position + handleTransform.right * side * HandSpread * .5f * s;
                    hand.Place(point, -body.forward, Vector3.up);
                }
                return;
            }

            // Flying: the glider moves the body, so leave the body alone
            rig.active = false;

            // Steer the way you'd hold it in VR: hands overhead and apart; A/D tilts them to roll,
            // W/S pushes them forward or pulls them back
            Vector3 input = InputHandler.inputDirection;
            Vector3 center = head.position + (Vector3.up * HandHeight + body.forward * (HandForward + input.z * PitchReach)) * s;
            foreach (var hand in new[] { leftHand, rightHand })
            {
                float side = hand.isLeft ? -1 : 1;
                Vector3 position = center + (body.right * side * HandSpread + Vector3.up * -side * input.x * RollHeight) * s;
                hand.Place(position, -body.forward, Vector3.up);
            }
        }
    }
}
