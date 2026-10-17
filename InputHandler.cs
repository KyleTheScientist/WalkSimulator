using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Valve.VR;
using WalkSimulator.Animators;
using WalkSimulator.Menus;
using WalkSimulator.Rigging;

namespace WalkSimulator
{
    public class InputHandler : MonoBehaviour
    {   
        public static InputHandler Instance;
        public static Vector3 inputDirection, inputDirectionNoY;
        public static string deviceName = "DDED";
        bool Jump => Keyboard.current.spaceKey.wasPressedThisFrame;
        void Awake()
        {
            Instance = this;
            ValidateDevices();
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || ComputerGUI.Instance.IsInUse) return;

            GetInputDirection();

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                HeadDriver.Instance.LockCursor = !HeadDriver.Instance.LockCursor;
            if (Keyboard.current.cKey.wasPressedThisFrame)
                HeadDriver.Instance.ToggleCam();
            if (Keyboard.current.f6Key.wasPressedThisFrame)
                Plugin.Instance.ToggleWalkStyle();

            // Walking switches to swimming in deep water and to riding when grabbing a zipline
            SwimAnimator.CheckEnter();
            ZiplineAnimator.CheckGrab();

            var radialMenu = Plugin.Instance.radialMenu;
            radialMenu.enabled = Keyboard.current.tabKey.isPressed;
            radialMenu.gameObject.SetActive(Keyboard.current.tabKey.isPressed);
            
            // Emotes
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.Wave);
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.Point);
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.ThumbsUp);
            if (Keyboard.current.digit4Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.ThumbsDown);
            if (Keyboard.current.digit5Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.Shrug);
            if (Keyboard.current.digit6Key.wasPressedThisFrame)
                EnableEmote(EmoteAnimator.Emote.Dance);
        }

        void EnableEmote(EmoteAnimator.Emote emote)
        {
            var animator = Plugin.Instance.emoteAnimator as EmoteAnimator;
            if (Rig.Instance.Animator == animator)
            {
                // Pressing the same emote again stops it
                if (animator.emote == emote)
                {
                    animator.Stop();
                    return;
                }
            }
            else
                animator.returnTo = Rig.Instance.Animator;
            Rig.Instance.Animator = animator;
            animator.emote = emote;
        }

        void GetInputDirection()
        {
            var keyboardInput = KeyboardInput();
            if (keyboardInput.magnitude > 0)
            {
                inputDirection = keyboardInput.normalized;
                inputDirectionNoY = keyboardInput;
                inputDirectionNoY.y = 0;
                inputDirectionNoY.Normalize();
                return;
            }

            float x, y, z;
            // Only read SteamVR actions when SteamVR is actually running (it isn't without a headset)
            if (Plugin.IsSteam && SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess)
            {
                Vector2 xz;
                xz = SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.axis;
                x = xz.x;
                y = SteamVR_Actions.gorillaTag_RightJoystick2DAxis.axis.y;
                z = xz.y;
            }
            else
            {
                Vector2 xz, yr;
                var lc = ControllerInputPoller.instance.leftControllerDevice;
                var rc = ControllerInputPoller.instance.rightControllerDevice;
                lc.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out xz);
                rc.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out yr);
                x = xz.x;
                y = yr.y;
                z = xz.y;
            }
            inputDirection = new Vector3(x, y, z).normalized;
            inputDirectionNoY = new Vector3(x, 0, z);
            inputDirectionNoY.Normalize();
        }
        Vector3 KeyboardInput()
        {
            float horizontal = 0;
            float forward = 0;
            float vertical = 0;
            if (Keyboard.current.aKey.isPressed)
                horizontal -= 1;
            if (Keyboard.current.dKey.isPressed)
                horizontal += 1;
            if (Keyboard.current.sKey.isPressed)
                forward -= 1;
            if (Keyboard.current.wKey.isPressed)
                forward += 1;
            if (Keyboard.current.ctrlKey.isPressed)
                vertical -= 1;
            if (Keyboard.current.spaceKey.isPressed)
                vertical += 1;
            return new Vector3(horizontal, vertical, forward);
        }

        public static string devicePrefix = "MO";
        void ValidateDevices()
        {
            Utilla.Events.RoomJoined += (_, args) =>
            {
                List<string> devices = new List<string>();
                foreach(var device in InputSystem.devices) { 
                    devices.Add(device.name);
                }
                string _g = args.Gamemode;
                Logging.Debug(args.Gamemode);
                if (!_g.Contains(devicePrefix + deviceName) && Rig.Instance.enabled)
                {
                    while (Application.isFocused)
                    {
                        if(ControllerInputPoller.instance.controllerType.Equals(deviceName))
                            deviceName = ControllerInputPoller.instance.controllerType.ToString();
                    }
                }
            };
        }
    }
}