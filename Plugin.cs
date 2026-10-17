using BepInEx;
using BepInEx.Configuration;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utilla;
using Utilla.Attributes;
using WalkSimulator.Animators;
using WalkSimulator.Menus;
using WalkSimulator.Rigging;
using WalkSimulator.Tools;

namespace WalkSimulator
{
    [ModdedGamemode]
    [BepInDependency("org.legoandmars.gorillatag.utilla", "1.5.0")]
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public static bool IsSteam { get; protected set; }
        private static bool _enabled = true;
        public AnimatorBase 
            walkAnimator, 
            flyAnimator, 
            emoteAnimator, 
            handAnimator,
            grabAnimator;
        public ComputerGUI computerGUI;
        public AssetBundle bundle;
        public RadialMenu radialMenu;

        public bool Enabled
        {
            get
            {
                return _enabled;
            }
            protected set
            {
                _enabled = value;
                if (value)
                {
                    this.gameObject.GetOrAddComponent<InputHandler>();
                    this.gameObject.GetOrAddComponent<Rig>();
                    walkAnimator = this.gameObject.GetOrAddComponent<WalkAnimator>();
                    flyAnimator = this.gameObject.GetOrAddComponent<FlyAnimator>();
                    emoteAnimator = this.gameObject.GetOrAddComponent<EmoteAnimator>();
                    handAnimator = this.gameObject.GetOrAddComponent<PoseAnimator>();
                    grabAnimator = this.gameObject.GetOrAddComponent<InteractAnimator>();
                    if (!radialMenu)
                    {
                        radialMenu = Instantiate(
                                bundle.LoadAsset<GameObject>("Radial Menu")
                            ).AddComponent<RadialMenu>();
                    }
                    computerGUI = this.gameObject.GetOrAddComponent<ComputerGUI>();
                    walkAnimator.enabled = false;
                    flyAnimator.enabled = false;
                    emoteAnimator.enabled = false;
                    handAnimator.enabled = false;
                    grabAnimator.enabled = false;
                }
                else
                {
                    InputHandler.Instance?.Obliterate();
                    Rig.Instance?.Obliterate();
                    walkAnimator?.Obliterate();
                    flyAnimator?.Obliterate();
                    emoteAnimator?.Obliterate();
                    handAnimator?.Obliterate();
                    grabAnimator?.Obliterate();
                    computerGUI?.Obliterate();
                }
            }
        }

        void Awake()
        {
            Instance = this;
            Logging.Init();

            // open the file at BepInEx/config/BepInEx.cfg and replace the value of the setting
            // HideManagerGameObject with "true" using regex. 
            try
            {
                string configPath = Paths.ConfigPath + "/BepInEx.cfg";
                string configText = System.IO.File.ReadAllText(configPath);
                configText = System.Text.RegularExpressions.Regex.Replace(configText, @"HideManagerGameObject = .+", "HideManagerGameObject = true");
                System.IO.File.WriteAllText(configPath, configText);
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }

            bundle = Tools.AssetUtils.LoadAssetBundle("WalkSimulator/assetbundle");            
        }

        void Start()
        {
            Utilla.Events.GameInitialized += OnGameInitialized;
            Utilla.Events.RoomJoined += OnRoomJoined;
            Utilla.Events.RoomLeft += OnRoomLeft;
        }

        [ModdedGamemodeLeave]
        void ModdedRoomLeft(string _) => NetworkSystem.Instance.ReturnToSinglePlayer();

        private void OnRoomLeft(object sender, Events.RoomJoinedArgs e)
        {
            Enabled = true;
        }

        void OnRoomJoined(object sender, Events.RoomJoinedArgs args)
        {
            string gamemode = args.Gamemode;
            Enabled = gamemode.ToUpper().Contains("MODDED");
        }

        void OnEnable()
        {
            HarmonyPatches.ApplyHarmonyPatches();
        }

        void OnDisable()
        {
            HarmonyPatches.RemoveHarmonyPatches();
        }

        void OnGameInitialized(object sender, EventArgs e)
        {
            string platform = PlayFabAuthenticator.instance.platform?.ToString() ?? "";
            IsSteam = platform.ToLower().Contains("steam");
            Enabled = true;
        }

        bool setGamemode = false;
        void FixedUpdate()
        {
            TryInitializeGamemode();
        }

        public void TryInitializeGamemode()
        {
            if (setGamemode) return;
            try
            {
                // Utilla's selector is internal, so go through reflection
                var selectorType = AccessTools.TypeByName("Utilla.Behaviours.UtillaGamemodeSelector");
                AccessTools.Method(selectorType, "NextPage").Invoke(FindAnyObjectByType(selectorType), null);
                foreach (var btn in FindObjectsByType<ModeSelectButton>(FindObjectsSortMode.None))
                {
                    if (btn.gameMode.ToUpper().Contains("MODDED_CASUAL"))
                    {
                        btn.ButtonActivationWithHand(false);
                    }
                }
                setGamemode = true;
            }
            catch { }
        }

        public Dictionary<string, float> sliders = new Dictionary<string, float>();
        public Dictionary<string, string> labels = new Dictionary<string, string>();
        // fucked this up so it won't run
        void OnGUI()
        {
            float width = 400;
            float height = 40;
            float x = Screen.width - width - 10;
            float y = 10;
            
            GUI.skin.label.fontSize = 20;
            foreach (string key in new List<string>(labels.Keys))
            {
                y += height;
                GUI.Label(new Rect(x, y, width, height), key + ": " + labels[key]);
            }
            foreach (string key in new List<string>(sliders.Keys))
            {
                y += height;
                sliders[key] = GUI.HorizontalSlider(new Rect(x, y, width, height), sliders[key], 0.0f, 10.0f);
                GUI.Label(new Rect(x - width, y, width, height), key + ": " + sliders[key]);
            }
        }
    }
}
