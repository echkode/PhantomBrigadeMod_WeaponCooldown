using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Combat.Systems;
using PBCIViewCombatTimeline = CIViewCombatTimeline;
using PBCombatUIUtility = PhantomBrigade.CombatUIUtility;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(CombatUILinkInWorldScrubbing), "Execute", new System.Type[] { typeof(List<CombatEntity>) })]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Cuilws_ExecuteTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Replace logic to get action duration with a call to CombatUIUtility.GetPaintedTimePlacementDuration().
			// This mod tinkers with action duration so I had to look everywhere that it's accessed and found this
			// redundant logic. There's already a function to get action duration so it makes sense to call it here.

			var cm = new CodeMatcher(instructions, generator);
			var getSelectedActionMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(InputContext), nameof(InputContext.selectedAction));
			var paintedActionChangeMethodInfo = AccessTools.DeclaredMethod(typeof(PBCIViewCombatTimeline), nameof(PBCIViewCombatTimeline.OnPaintedActionChange));
			var selectedActionMatch = new CodeMatch(OpCodes.Callvirt, getSelectedActionMethodInfo);
			var callMatch = new CodeMatch(OpCodes.Call);
			var paintedActionChangeMatch = new CodeMatch(OpCodes.Callvirt, paintedActionChangeMethodInfo);
			var loadStaticFieldMatch = new CodeMatch(OpCodes.Ldsfld);
			var callGetDuration = CodeInstruction.Call(typeof(PBCombatUIUtility), nameof(PBCombatUIUtility.GetPaintedTimePlacementDuration));

			cm.MatchEndForward(selectedActionMatch)
				.MatchEndForward(callMatch)
				.Advance(2);
			var deleteStart = cm.Pos;
			cm.MatchStartForward(paintedActionChangeMatch)
				.MatchStartBackwards(loadStaticFieldMatch);
			cm.Labels.Clear();
			cm.Advance(-2);
			var offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0)
				.Advance(offset)
				.Insert(callGetDuration);

			return cm.InstructionEnumeration();
		}
	}
}
