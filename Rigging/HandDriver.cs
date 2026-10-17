using GorillaLocomotion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WalkSimulator.Patches;
using WalkSimulator.Tools;

namespace WalkSimulator.Rigging
{
    public class HandDriver : MonoBehaviour
    {
        public bool grip, trigger, primary, secondary;
        public bool isLeft, grounded;
        public bool hideControllerTransform = true;
        public Vector3 targetPosition, lookAt, up;
        public float followRate = .1f;
        public Vector3 hit, lastSnap, normal;

        VRMap handMap;
        Vector3 defaultOffset;
        Transform head, body, controller;

        //void Start()
        //{
            
        //}

        public void Init(bool isLeft)
        {
            this.isLeft = isLeft;
            defaultOffset = new Vector3(isLeft ? -.25f : .25f, -.45f, .2f);
            head = Rig.Instance.head;
            body = Rig.Instance.body;
            controller = isLeft ?
                Player.Instance.leftControllerTransform :
                Player.Instance.rightControllerTransform;
            handMap = isLeft ?
                GorillaTagger.Instance.offlineVRRig.leftHand :
                GorillaTagger.Instance.offlineVRRig.rightHand;
            transform.position = DefaultPosition;
            targetPosition = DefaultPosition;
            lastSnap = DefaultPosition;
            hit = DefaultPosition;
            up = Vector3.up;
        }

        void FixedUpdate()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, followRate);
            transform.LookAt(lookAt, up);
            if(hideControllerTransform)
                controller.position = body.position;
            else
                controller.position = transform.position;

            //DebugPoint.Get(this.name + "target", Color.red).position = targetPosition;
            //DebugPoint.Get(this.name + "hit", Color.blue).position = hit;
            //DebugPoint.Get(this.name + "snap", Color.green).position = lastSnap;
            //DebugPoint.Get(this.name + "lookAt", lookAt,  Color.magenta);
            //DebugPoint.Get(this.name + "up", targetPosition + up * .1f, Color.cyan);


            if (isLeft)
            {
                FingerPatch.forceLeftGrip = grip;
                FingerPatch.forceLeftPrimary = primary;
                FingerPatch.forceLeftSecondary = secondary;
                FingerPatch.forceLeftTrigger = trigger;
            }
            else
            {
                FingerPatch.forceRightGrip = grip;
                FingerPatch.forceRightPrimary = primary;
                FingerPatch.forceRightSecondary = secondary;
                FingerPatch.forceRightTrigger = trigger;
            }
        }

        void OnEnable()
        {
            if (!body || Rig.Instance.Animator is null) return;
            transform.position = DefaultPosition;
            targetPosition = DefaultPosition;
            try
            {
                handMap.overrideTarget = this.transform;
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        IEnumerator Disable(Action<HandDriver> onDisable)
        {
            transform.position = DefaultPosition;
            yield return new WaitForSeconds(0.1f);
            handMap.overrideTarget = null;
            this.enabled = false;
            onDisable?.Invoke(this);
        }

        public void Reset()
        {
            grip = trigger = primary = false;
            targetPosition = DefaultPosition;
            lookAt = targetPosition + body.forward;
            up = isLeft ? body.right : -body.right;
            hideControllerTransform = true;
            grounded = false;
        }
        

        public Vector3 DefaultPosition { get { return body.TransformPoint(defaultOffset); } }


    }
}
