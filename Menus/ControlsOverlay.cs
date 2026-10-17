using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WalkSimulator.Animators;
using WalkSimulator.Rigging;

namespace WalkSimulator.Menus
{
    /// <summary>
    /// Lists the controls for the current mode in the bottom-left corner. H toggles it.
    /// </summary>
    public class ControlsOverlay : MonoBehaviour
    {
        GUIStyle boxStyle, textStyle;

        void Update()
        {
            if (Keyboard.current.hKey.wasPressedThisFrame && !ComputerGUI.Instance.IsInUse)
                Plugin.ShowControls.Value = !Plugin.ShowControls.Value;
        }

        List<(string key, string action)> CurrentControls()
        {
            var lines = new List<(string, string)>();
            var plugin = Plugin.Instance;
            var animator = Rig.Instance ? Rig.Instance.Animator : null;

            if (ComputerGUI.Instance && ComputerGUI.Instance.IsInUse)
            {
                lines.Add(("Computer", ""));
                lines.Add(("Letters / 0-9", "Type"));
                lines.Add(("F1 / F2 / F3", "Option buttons"));
                lines.Add(("Enter", "Enter"));
                lines.Add(("Backspace", "Delete"));
                lines.Add(("Up / Down", "Change screen"));
                lines.Add(("Esc", "Leave the computer"));
                return lines;
            }

            if (plugin.radialMenu && plugin.radialMenu.isActiveAndEnabled)
            {
                lines.Add(("Mode menu", ""));
                lines.Add(("Mouse", "Point at a mode"));
                lines.Add(("Release Tab", "Choose it"));
                return lines;
            }

            if (!animator)
            {
                lines.Add(("VR mode", ""));
                lines.Add(("V", "Switch to desktop controls"));
                lines.Add(("Tab", "Choose a desktop mode"));
                lines.Add(("H", "Hide controls"));
                return lines;
            }

            if (animator == plugin.proceduralWalkAnimator || animator == plugin.classicWalkAnimator)
            {
                bool classic = animator == plugin.classicWalkAnimator;
                lines.Add((classic ? "Walk (classic)" : "Walk", ""));
                lines.Add(("WASD", "Move"));
                lines.Add(("Mouse", "Look"));
                lines.Add(("Shift", "Sprint"));
                lines.Add(("Space", "Jump"));
                if (!classic)
                    lines.Add(("Space + into wall", "Wall jump (repeat to climb)"));
                lines.Add(("Ctrl", "Crouch"));
                lines.Add(("Hold click", "Grab a zipline, rope, glider or broom"));
                lines.Add(("F6", classic ? "Switch to new walk" : "Switch to classic walk"));
            }
            else if (animator == plugin.flyAnimator)
            {
                bool noClip = (plugin.flyAnimator as FlyAnimator).NoClip;
                lines.Add(("Fly", ""));
                lines.Add(("WASD", "Move"));
                lines.Add(("Space / Ctrl", "Up / down"));
                lines.Add(("Mouse", "Look"));
                lines.Add(("Shift", "Faster"));
                lines.Add(("Scroll", $"Flight speed ({(plugin.flyAnimator as FlyAnimator).Speed:0.##}x)"));
                lines.Add(("N", "Noclip: " + (noClip ? "on" : "off")));
            }
            else if (animator == plugin.swimAnimator)
            {
                lines.Add(("Swim", ""));
                lines.Add(("WASD", "Swim (toward where you look)"));
                lines.Add(("Space / Ctrl", "Up / down"));
                lines.Add(("Shift", "Faster"));
                lines.Add(("Space at surface", "Leap out"));
            }
            else if (animator == plugin.ziplineAnimator)
            {
                lines.Add(("Zipline", ""));
                lines.Add(("Release click", "Let go"));
                lines.Add(("Space", "Jump off"));
            }
            else if (animator == plugin.ropeAnimator)
            {
                lines.Add(("Rope", ""));
                lines.Add(("WASD", "Swing"));
                lines.Add(("Space", "Jump off"));
                lines.Add(("Release click", "Let go"));
            }
            else if (animator == plugin.gliderAnimator)
            {
                lines.Add(("Glider", ""));
                lines.Add(("A / D", "Roll (tilt hands)"));
                lines.Add(("W / S", "Dive / climb"));
                lines.Add(("On the ground", "WASD walk, Shift sprint, Space jump"));
                lines.Add(("Mouse", "Look / turn"));
                lines.Add(("Release click", "Let go"));
            }
            else if (animator == plugin.broomAnimator)
            {
                lines.Add(("Broomstick", ""));
                lines.Add(("Release click", "Let go"));
                lines.Add(("Space", "Jump off"));
            }
            else if (animator == plugin.emoteAnimator)
            {
                lines.Add(("Emote", ""));
                lines.Add(("Move / same number", "Stop"));
            }
            else if (animator == plugin.handAnimator)
            {
                lines.Add(("Pose", ""));
                lines.Add(("Mouse", "Move hand"));
                lines.Add(("Scroll / Up / Down", "Hand forward / back"));
                lines.Add(("Hold R + mouse", "Rotate hand (scroll rolls)"));
                lines.Add(("Q", "Switch hand"));
                lines.Add(("Left / right click", "Trigger / grip"));
                lines.Add(("[ / ]", "Thumb buttons"));
            }
            else if (animator == plugin.grabAnimator)
            {
                lines.Add(("Interact", ""));
                lines.Add(("Left / right click", "Press a button with left / right hand"));
                lines.Add(("Hold click + mouse", "Drag a slider up / down"));
            }

            lines.Add(("", ""));
            lines.Add(("Tab", "Mode menu"));
            lines.Add(("1-6", "Emotes"));
            lines.Add(("C", "First / third person"));
            lines.Add(("Esc", "Free / lock cursor"));
            lines.Add(("E", "Use computer (when near one)"));
            lines.Add(("V", "Switch to VR"));
            lines.Add(("H", "Hide controls"));
            return lines;
        }

        void OnGUI()
        {
            if (!Plugin.ShowControls.Value || !Plugin.Instance.Enabled) return;

            if (boxStyle == null)
            {
                var background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0, 0, 0, .55f));
                background.Apply();
                boxStyle = new GUIStyle() { normal = new GUIStyleState() { background = background } };
                textStyle = new GUIStyle()
                {
                    fontSize = 16,
                    richText = true,
                    normal = new GUIStyleState() { textColor = Color.white }
                };
            }

            var lines = CurrentControls();
            const float lineHeight = 22, padding = 10, keyWidth = 170, width = 470;
            float height = lines.Count * lineHeight + padding * 2;
            var area = new Rect(20, Screen.height - height - 20, width, height);
            GUI.Box(area, GUIContent.none, boxStyle);

            float y = area.y + padding;
            foreach (var (key, action) in lines)
            {
                if (action == "" && key != "")
                    GUI.Label(new Rect(area.x + padding, y, width, lineHeight), $"<b><size=18>{key}</size></b>", textStyle);
                else
                {
                    GUI.Label(new Rect(area.x + padding, y, keyWidth, lineHeight), $"<b>{key}</b>", textStyle);
                    GUI.Label(new Rect(area.x + padding + keyWidth, y, width - keyWidth, lineHeight), action, textStyle);
                }
                y += lineHeight;
            }
        }
    }
}
