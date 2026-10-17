using GorillaLocomotion;
using GorillaNetworking;
using System.Collections;
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
            int buttonLayer = LayerMask.GetMask("GorillaInteractable", "TransparentFX");
            var hits = Physics.RaycastAll(ray, .82f, buttonLayer);
            foreach (var hit in hits)
            {
                if (hit.transform.GetComponent<GorillaPressableButton>()
                    || hit.transform.GetComponent<GorillaKeyboardButton>()
                    || hit.transform.GetComponent<GorillaPlayerLineButton>()
                    || hit.transform.name.ToLower().Contains("button"))
                    yield return PressButton(main, hit.point - Camera.main.transform.forward * .05f);
            }
            state = State.IDLE;
        }

        IEnumerator PressButton(HandDriver hand, Vector3 targetPosition)
        {
            state = State.BUTTON;
            hand.grip = true;
            hand.targetPosition = reticle.position;
            hand.lookAt = targetPosition;
            hand.up = hand.isLeft ? head.right : -head.right;
            // move the targetPosition from the reticle position to the button position
            yield return new WaitForSeconds(.1f);

            hand.targetPosition = targetPosition;
            while (hand.transform.position.Distance(targetPosition) > .05f)
                yield return new WaitForFixedUpdate();
            hand.targetPosition = reticle.position + Camera.main.transform.forward * .05f;

            yield return new WaitForSeconds(.1f);

            hand.targetPosition = hand.DefaultPosition;
            state = State.IDLE;
            yield break;
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
