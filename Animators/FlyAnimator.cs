using GorillaLocomotion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator.Animators
{
    public class FlyAnimator : AnimatorBase
    {
        float speed = 1;
        float minSpeed = 0;
        float maxSpeed = 5;
        int layersBackup;
        bool noClipActive = false;

        void Awake()
        {
            layersBackup = Player.Instance.locomotionEnabledLayers;
        }

        public override void Animate()
        {
            AnimateBody();
            AnimateHands();
        }

        void Update()
        {
            speed += Mouse.current.scroll.ReadValue().y / 1000f;
            speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
            if(Keyboard.current.nKey.wasPressedThisFrame)
            {
                noClipActive = !noClipActive;
                Player.Instance.locomotionEnabledLayers = noClipActive ? 1 << 29 : layersBackup;
                Player.Instance.headCollider.isTrigger = noClipActive;
                Player.Instance.bodyCollider.isTrigger = noClipActive;
            }
        }

        void AnimateBody()
        {
            rig.active = true;
            rig.useGravity = false;
            rig.targetPosition = body.TransformPoint(InputHandler.inputDirection * speed);
        }

        void AnimateHands()
        {
            leftHand.followRate = rightHand.followRate = Extensions.Map(speed, minSpeed, maxSpeed, 0, 1);
            leftHand.targetPosition = leftHand.DefaultPosition;
            rightHand.targetPosition = rightHand.DefaultPosition;
            leftHand.lookAt = leftHand.targetPosition + body.forward;
            rightHand.lookAt = rightHand.targetPosition + body.forward;
            leftHand.up = body.right;
            rightHand.up = -body.right;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            leftHand.followRate = rightHand.followRate = .1f;
            Player.Instance.locomotionEnabledLayers = layersBackup;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
        }
    }
}
