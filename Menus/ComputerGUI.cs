using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using WalkSimulator.Animators;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;
using WalkSimulatorClassic;
using Thirpy = Cinemachine.Cinemachine3rdPersonFollow;

namespace WalkSimulator.Menus
{
    public class ComputerGUI : MonoBehaviour
    {
        public static ComputerGUI Instance;
        GorillaComputerTerminal[] terminals = new GorillaComputerTerminal[0];
        AnimatorBase cachedAnimator;
        Transform currentTerminal;
        Camera overrideCam;

        public bool IsInUse => overrideCam.enabled;

        bool inRange;
        bool IsInRange()
        {
            Vector3 playerPos = Player.Instance.bodyCollider.transform.position;
            if (playerPos.Distance(GorillaComputer.instance.transform.position) < 2)
            {
                currentTerminal = GorillaComputer.instance.transform;
                return true;
            }
            foreach (var terminal in terminals)
            {
                if (playerPos.Distance(terminal.transform.position) < 2)
                {
                    currentTerminal = terminal.transform;
                    return true;
                }
            }
            currentTerminal = null;
            return false;
        }


        void Awake()
        {
            Instance = this;
            var cameraObject = new GameObject("WalkSim First Person Camera");
            var thirpy = FindObjectOfType<Thirpy>();
            var shoulderCamera = thirpy.gameObject.GetComponentInParent<Camera>();

            overrideCam = cameraObject.AddComponent<Camera>();
            overrideCam.fieldOfView = 90;
            overrideCam.nearClipPlane = shoulderCamera.nearClipPlane;
            overrideCam.farClipPlane = shoulderCamera.farClipPlane;
            overrideCam.targetDisplay = shoulderCamera.targetDisplay;
            overrideCam.cullingMask = shoulderCamera.cullingMask;
            overrideCam.depth = shoulderCamera.depth + 1;
            overrideCam.targetDisplay = shoulderCamera.targetDisplay;
            overrideCam.enabled = false;

        }

        void Start() => BuildButtonMap();

        void Update()
        {
            if (Keyboard.current.eKey.wasPressedThisFrame && inRange && !overrideCam.enabled)
            {
                overrideCam.enabled = true;
                cachedAnimator = Rig.Instance.Animator;
                Rig.Instance.Animator = null;
                return;
            }

            if (overrideCam.enabled)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    overrideCam.enabled = false;
                    Rig.Instance.Animator = cachedAnimator;
                    return;
                }
                foreach (var key in buttonMapping.Keys)
                {
                    try
                    {
                        if (key is null)
                        {
                            Logging.Debug("Key is null");
                        }
                        if (key.wasPressedThisFrame)
                        {
                            Logging.Debug("Pressed", key?.name);
                            GorillaComputer.instance.PressButton(buttonMapping[key]);
                            Sounds.Play(66, .5f);
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.Exception(e);
                    }
                }
            }
        }

        void FixedUpdate()
        {
            if (Time.frameCount % 60 == 0)
                inRange = IsInRange();

            if (!inRange && Time.frameCount % 600 == 0)
                terminals = FindObjectsOfType<GorillaComputerTerminal>();

            if (inRange)
            {
                Transform target;
                if (currentTerminal.GetComponent<GorillaComputerTerminal>() is GorillaComputerTerminal terminal)
                    target = terminal.myScreenText.transform;
                else
                    target = Traverse.Create(GorillaComputer.instance.screenText).Field("text").GetValue<Text>().transform;
                //DebugPoint.Get("Camera Anchor Forward", target.TransformPoint(Vector3.forward * -.3f), Color.blue);
                overrideCam.transform.position = target.TransformPoint(Vector3.forward * -.3f);
                overrideCam.transform.LookAt(target);
            }
            else
            {
                overrideCam.enabled = false;
            }
        }

        void OnGUI()
        {
            string text = "";
            if (inRange)
            {
                text = overrideCam.enabled ?
                    "Press [Escape] to exit" :
                    "Press [E] to use computer";
            }
            GUI.Label(new Rect(20, 20, 200, 200), text, new GUIStyle()
            {
                fontSize = 20,
                normal = new GUIStyleState()
                {
                    textColor = Color.white
                }
            });
            return;

        }

        Dictionary<KeyControl, GorillaKeyboardButton> buttonMapping = new Dictionary<KeyControl, GorillaKeyboardButton>();
        void BuildButtonMap()
        {
            foreach (var button in FindObjectsOfType<GorillaKeyboardButton>())
            {
                Key key = keyMapping[button.characterString];
                buttonMapping.Add(Keyboard.current[key], button);
            }
        }

        Dictionary<string, Key> keyMapping = new Dictionary<string, Key>()
        {
            {"A", Key.A},
            {"B", Key.B},
            {"C", Key.C},
            {"D", Key.D},
            {"E", Key.E},
            {"F", Key.F},
            {"G", Key.G},
            {"H", Key.H},
            {"I", Key.I},
            {"J", Key.J},
            {"K", Key.K},
            {"L", Key.L},
            {"M", Key.M},
            {"N", Key.N},
            {"O", Key.O},
            {"P", Key.P},
            {"Q", Key.Q},
            {"R", Key.R},
            {"S", Key.S},
            {"T", Key.T},
            {"U", Key.U},
            {"V", Key.V},
            {"W", Key.W},
            {"X", Key.X},
            {"Y", Key.Y},
            {"Z", Key.Z},
            {"0", Key.Digit0},
            {"1", Key.Digit1},
            {"2", Key.Digit2},
            {"3", Key.Digit3},
            {"4", Key.Digit4},
            {"5", Key.Digit5},
            {"6", Key.Digit6},
            {"7", Key.Digit7},
            {"8", Key.Digit8},
            {"9", Key.Digit9},
            {"option1", Key.F1},
            {"option2", Key.F2},
            {"option3", Key.F3},
            {"enter", Key.Enter},
            {"delete", Key.Backspace},
            {"up", Key.UpArrow},
            {"down", Key.DownArrow},

        };
    }
}
