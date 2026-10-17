using HarmonyLib;
using WalkSimulator.Animators;

namespace WalkSimulator.Patches
{
    /// <summary>
    /// The game only grabs an item whose interaction point its hand collider is overlapping, which
    /// doesn't always register for the mod's hands. While the glider mode is reaching for a handle,
    /// slip it into the overlap list right before the game checks it, so the normal grab runs.
    /// </summary>
    [HarmonyPatch(typeof(EquipmentInteractor), "LateUpdate")]
    public class EquipmentInteractorPatch
    {
        static void Prefix(EquipmentInteractor __instance)
        {
            if (!Plugin.Instance.Enabled) return;
            var left = GliderAnimator.injectLeft;
            var right = GliderAnimator.injectRight;
            if (left && !__instance.overlapInteractionPointsLeft.Contains(left))
                __instance.overlapInteractionPointsLeft.Add(left);
            if (right && !__instance.overlapInteractionPointsRight.Contains(right))
                __instance.overlapInteractionPointsRight.Add(right);
        }
    }
}
