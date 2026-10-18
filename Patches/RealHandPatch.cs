using GorillaLocomotion;
using HarmonyLib;
using WalkSimulator.Rigging;

namespace WalkSimulator.Patches
{
    /// <summary>
    /// The game moves the player's physical hands from the controller transforms in GTPlayer.LateUpdate,
    /// but the mod's earlier writes to those transforms get overwritten (by the controller tracking)
    /// before then. For hands that need real contact with the world (e.g. paddling a hoverboard), set
    /// the controller transform right before the game reads it.
    /// </summary>
    [HarmonyPatch(typeof(GTPlayer), "LateUpdate")]
    public class RealHandPatch
    {
        static void Prefix(GTPlayer __instance)
        {
            if (!Plugin.Instance.Enabled || !Rig.Instance) return;
            foreach (var hand in new[] { Rig.Instance.leftHand, Rig.Instance.rightHand })
            {
                if (!hand || !hand.enabled || !hand.driveRealHand) continue;
                var controller = __instance.GetControllerTransform(hand.isLeft);
                if (controller)
                    controller.SetPositionAndRotation(hand.transform.position, hand.transform.rotation);
            }
        }
    }
}
