using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator.Animators
{
    public class WalkAnimator : AnimatorBase
    {
        float speed = 4f;
        float height = .4f;
        float targetHeight;

        public override void Animate()
        {
            MoveBody();
            AnimateHands();
        }

        bool hasJumped, onJumpCooldown;
        float jumpTime;
        void Update()
        {
            if (!Plugin.Instance.Enabled) return;

            if (!hasJumped && rig.onGround && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                hasJumped = true;
                onJumpCooldown = true;
                jumpTime = Time.time;
                rig.active = false;
                rigidbody.AddForce(Vector3.up * 250 * GTPlayer.Instance.scale, ForceMode.Impulse);
            }
            if (hasJumped && !rig.onGround || Time.time - jumpTime > 1)
                onJumpCooldown = false;
            if (rig.onGround && !onJumpCooldown)
                hasJumped = false;
        }

        public void MoveBody()
        {
            rig.active = rig.onGround && !hasJumped;
            rig.useGravity = !rig.onGround;
            if (!rig.onGround) return;

            // Idle breathing, or bobbing up and down while walking
            float baseHeight, bob, t;
            if (NotMoving)
            {
                baseHeight = .5f;
                bob = .04f;
                t = Mathf.Sin(Time.time * Mathf.PI * 2) * .5f + .5f;
            }
            else
            {
                baseHeight = .46f;
                bob = IsSprinting ? .22f : .16f;
                // Lowest when a hand plants, highest mid-swing (one bob per step)
                t = Mathf.Sin(walkCycleTime * Mathf.PI);
            }
            if (Keyboard.current.ctrlKey.isPressed)
                baseHeight -= .3f;
            targetHeight = baseHeight + bob * t;
            // Ease toward the target so the body doesn't snap between heights
            height = Mathf.Lerp(height, targetHeight, Extensions.Smoothing(.3f));
            Vector3 targetPosition = rig.lastGroundPosition + Vector3.up * height * GTPlayer.Instance.scale;

            // Handle movement
            Vector3 direction = body.TransformDirection(InputHandler.inputDirectionNoY);
            direction.y = 0;
            if (Vector3.Dot(rig.lastNormal, Vector3.up) > .3f) // If the slope is too steep, don't correct the direction
                direction = Vector3.ProjectOnPlane(direction, rig.lastNormal);
            direction *= GTPlayer.Instance.scale;
            float adjSpeed = IsSprinting ? speed * 3f : speed;
            targetPosition += direction * adjSpeed / 10f;
            rig.targetPosition = targetPosition;
        }

        void AnimateHands()
        {
            // Hands have to cover more ground per step when sprinting, so let them catch up faster
            leftHand.followRate = rightHand.followRate = IsSprinting ? .35f : .1f;
            leftHand.lookAt = leftHand.targetPosition + body.forward;
            rightHand.lookAt = rightHand.targetPosition + body.forward;
            leftHand.up = body.right;
            rightHand.up = -body.right;
            if (!rig.onGround)
            {
                leftHand.grounded = false;
                rightHand.grounded = false;
                //leftHand.lastSnap = leftHand.DefaultPosition;
                //rightHand.lastSnap = rightHand.DefaultPosition;
                //leftHand.hit = leftHand.DefaultPosition;
                //rightHand.hit = rightHand.DefaultPosition;
                Vector3 offset = Vector3.up * .2f * GTPlayer.Instance.scale;
                leftHand.targetPosition = leftHand.DefaultPosition;
                rightHand.targetPosition = rightHand.DefaultPosition + offset;
                return;
            }

            UpdateHitInfo(leftHand);
            UpdateHitInfo(rightHand);

            if (NotMoving)
            {
                leftHand.targetPosition = leftHand.hit;
                rightHand.targetPosition = rightHand.hit;
                return;
            }

            if (!leftHand.grounded && !rightHand.grounded)
            {
                leftHand.grounded = true;
                leftHand.lastSnap = leftHand.hit;
                leftHand.targetPosition = leftHand.hit;
                rightHand.lastSnap = rightHand.hit;
                rightHand.targetPosition = rightHand.hit;
            }
            AnimateHand(leftHand, rightHand);
            AnimateHand(rightHand, leftHand);
        }

        void UpdateHitInfo(HandDriver hand)
        {
            float scale = GTPlayer.Instance.scale;
            float raycastLength = .5f * scale;
            var groundPos = rig.smoothedGroundPosition;
            var groundNormal = rig.lastNormal;

            float forwardness = Mathf.Abs(Vector3.Dot(InputHandler.inputDirectionNoY, Vector3.forward));
            float adjOriginDistance = Extensions.Map(forwardness, 0, 1, .4f, .5f);
            var directionOffset = body.TransformDirection(InputHandler.inputDirectionNoY * adjOriginDistance);
            directionOffset.y = 0;
            directionOffset *= scale;
            Vector3 origin = Vector3.ProjectOnPlane(hand.DefaultPosition - groundPos + directionOffset, groundNormal);
            origin += groundPos + (groundNormal * .3f * scale);
            //DebugPoint.Get(hand.name + "raycast origin", Color.cyan).position = origin;
            //DebugPoint.Get(hand.name + "raycast end", Color.yellow).position = origin + -groundNormal * raycastLength;

            RaycastHit hit;
            if (!Physics.Raycast(origin, -groundNormal, out hit, raycastLength, GTPlayer.Instance.locomotionEnabledLayers))
            {
                if (NotMoving)
                    hand.targetPosition = hand.DefaultPosition;
                return;
            }
            hand.hit = hit.point;
            hand.normal = hit.normal;
            hand.lookAt = hand.transform.position + body.forward;
        }

        float walkCycleTime = 0;
        void AnimateHand(HandDriver hand, HandDriver otherHand)
        {
            float forwardness = Mathf.Abs(Vector3.Dot(InputHandler.inputDirectionNoY, Vector3.forward));
            float flatness = Vector3.Dot(rig.lastNormal, Vector3.up);
            float snapDistance = Extensions.Map(flatness, 0, 1, .1f, .6f);
            float adjSnapDistance = Extensions.Map(forwardness, 0, 1, .5f, 1.25f);
            adjSnapDistance *= snapDistance * GTPlayer.Instance.scale;
            float stepHeight = .2f * GTPlayer.Instance.scale;

            float otherStretch = otherHand.hit.Distance(otherHand.lastSnap) / adjSnapDistance;
            if (otherHand.grounded && otherStretch >= 1)
            {
                hand.targetPosition = hand.hit;
                hand.lastSnap = hand.hit;
                hand.grounded = true;
                otherHand.grounded = false;
            }
            else if (otherHand.grounded)
            {
                walkCycleTime = Mathf.Clamp01(otherStretch);
                // Lerp, not Slerp: these are points, and Slerp would arc them around the world origin
                hand.targetPosition = Vector3.Lerp(hand.lastSnap, hand.hit, walkCycleTime);
                // Lift peaks mid-swing and is back on the ground when the hand plants
                hand.targetPosition += hand.normal * stepHeight * Mathf.Sin(walkCycleTime * Mathf.PI);
                //hand.targetPosition = hand.lastSnap;
                hand.grounded = false;
            }

            if (hand.targetPosition.Distance(hand.DefaultPosition) > 1f)
            {
                hand.targetPosition = hand.DefaultPosition;
            }
            //hand.followRate = hand.grounded ? .2f : .1f;
            //DebugPoint.Get($"{hand.name} hit", hand.isLeft ? Color.blue : Color.green).position = hand.hit;
            //DebugPoint.Get($"{hand.name} snap", hand.isLeft ? Color.cyan : Color.magenta).position = hand.lastSnap;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            leftHand.followRate = rightHand.followRate = .1f;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
        }

        bool IsSprinting
        {
            get { return Keyboard.current.leftShiftKey.isPressed; }
        }

        bool NotMoving
        {
            get { return InputHandler.inputDirectionNoY == Vector3.zero; }
        }
    }
}