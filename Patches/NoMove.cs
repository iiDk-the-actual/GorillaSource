using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace GorillaSource.Patches
{
    [HarmonyPatch(typeof(GorillaLocomotion.GTPlayer))]
    [HarmonyPatch("LateUpdate", MethodType.Normal)]
    internal class NoMove
    {
        private static void Prefix()
        {
            if (Plugin.instance.isSource)
            {
                Traverse.Create(GorillaLocomotion.GTPlayer.Instance).Field("leftHandHolding").SetValue(true);
                Traverse.Create(GorillaLocomotion.GTPlayer.Instance).Field("rightHandHolding").SetValue(true);
            }
        }
    }
}
