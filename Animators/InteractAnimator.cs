using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator.Animators
{
    public class InteractAnimator : AnimatorBase
    {
        public enum State
        {
            IDLE, WAIT, BUTTON, GRAB
        }

        Transform reticle;
        State state;

        public override void Animate()
        {
            reticle.gameObject.SetActive(true);
            HeadDriver.Instance.FirstPerson = true;
            AnimateBody();
            AnimateHands();
        }

        void Update()
        {
            if (state != State.IDLE) return;
            if (Mouse.current.leftButton.isPressed)
            {
                state = State.WAIT;
                StartCoroutine(Raycast(leftHand));
            }
            else if (Mouse.current.rightButton.isPressed)
            {
                state = State.WAIT;
                StartCoroutine(Raycast(rightHand));
            }
        }

        void AnimateBody()
        {
            rig.active = true;
            rig.useGravity = false;
            rig.targetPosition = body.position;
        }

        IEnumerator Raycast(HandDriver main)
        {
            // raycast from the camera position in the direction of the reticle
            var ray = new Ray(Camera.main.transform.position, reticle.position - Camera.main.transform.position);
            // Buttons live on several layers (elevator buttons aren't on GorillaInteractable), and
            // most are triggers, so check everything and press only the nearest button
            var hits = Physics.RaycastAll(ray, .82f, ~0, QueryTriggerInteraction.Collide)
                .OrderBy(hit => hit.distance);
            foreach (var hit in hits)
            {
                var pressable = FindPressable(hit.transform);
                if (pressable.component
                    || hit.transform.GetComponent<GorillaKeyboardButton>()
                    || hit.transform.GetComponent<GorillaPlayerLineButton>()
                    || hit.transform.name.ToLower().Contains("button"))
                {
                    yield return PressButton(main, hit.point - ray.direction * .03f, pressable);
                    break;
                }
            }
            state = State.IDLE;
        }

        // Most buttons can be pressed directly with Click(isLeftHand) (GorillaPressableButton,
        // GamePressableButton, ...) or ButtonActivationWithHand(isLeftHand). Touching them with the
        // hand doesn't always register for newer buttons like the elevator's.
        static readonly string[] pressMethodNames = { "Click", "ButtonActivationWithHand" };
        static (Component component, MethodInfo method) FindPressable(Transform target)
        {
            foreach (var component in target.GetComponentsInParent<MonoBehaviour>())
            {
                if (!component) continue;
                foreach (var name in pressMethodNames)
                {
                    var method = AccessTools.Method(component.GetType(), name, new[] { typeof(bool) });
                    if (method != null)
                        return (component, method);
                }
            }
            return (null, null);
        }

        IEnumerator PressButton(HandDriver hand, Vector3 buttonPoint, (Component component, MethodInfo method) pressable)
        {
            state = State.BUTTON;

            // Press it right away
            if (pressable.component)
            {
                try
                {
                    pressable.method.Invoke(pressable.component, new object[] { hand.isLeft });
                }
                catch (Exception e) { Logging.Exception(e); }
            }

            // The rest is for show: a quick pointing tap from the hand's resting spot to the
            // button and back (the touch also presses buttons that have no click method)
            Vector3 toButton = buttonPoint - hand.DefaultPosition;
            hand.grip = true;   // grip without trigger makes a pointing finger
            hand.followRate = .5f;
            hand.targetPosition = buttonPoint;
            hand.lookAt = buttonPoint + toButton;
            hand.up = hand.isLeft ? head.right : -head.right;
            yield return new WaitForSeconds(.12f);

            hand.targetPosition = hand.DefaultPosition;
            yield return new WaitForSeconds(.1f);

            hand.grip = false;
            hand.followRate = .1f;
            state = State.IDLE;
        }

        void AnimateHands()
        {
            //main.targetPosition = targetPosition;
            if (state == State.IDLE)
            {
                leftHand.targetPosition = leftHand.DefaultPosition;
                rightHand.targetPosition = rightHand.DefaultPosition;
                leftHand.lookAt = leftHand.targetPosition + head.forward;
                rightHand.lookAt = rightHand.targetPosition + head.forward;
                leftHand.up = head.right;
                rightHand.up = -head.right;
            }
        }

        protected override void Start()
        {
            base.Start();
            reticle = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            reticle.localScale = Vector3.one * .001f;
            reticle.GetComponent<MeshRenderer>().material.color = Color.white;
            reticle.GetComponent<MeshRenderer>().material.shader = Shader.Find("GorillaTag/UberShader");
            reticle.GetComponent<SphereCollider>().enabled = false;
            reticle.transform.SetParent(Camera.main.transform);
            reticle.transform.localPosition = Vector3.forward * .1f;
            reticle.gameObject.SetActive(false);
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            reticle.gameObject.SetActive(false);
            state = State.IDLE;
            StopAllCoroutines();
        }

        void OnDestory()
        {
            Destroy(reticle.gameObject);
        }
    }
}
