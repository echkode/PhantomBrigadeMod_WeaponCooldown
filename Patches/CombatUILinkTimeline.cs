using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Combat.Systems;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(CombatUILinkTimeline), "Execute", new System.Type[] { typeof(List<ActionEntity>) })]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Cuilt_ExecuteTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Do not create UI objects for extrapolated moves. Extrapolated moves are synthetic actions
			// instantiated when a run action ends very close to the end of the turn. The code to configure
			// UI objects for actions skips extrapolated moves so what happens is that a UI object is created
			// for these stub moves but never gets configured.

			var cm = new CodeMatcher(instructions, generator);
			var hasDurationPropInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.hasDuration));
			var isExtrapolatedPropInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.isMovementExtrapolated));
			var hasDurationMatch = new CodeMatch(OpCodes.Callvirt, hasDurationPropInfo);
			var isExtrapolated = new CodeInstruction(OpCodes.Callvirt, isExtrapolatedPropInfo);

			cm.MatchStartForward(hasDurationMatch)
				.Advance(-1);
			var loadEntity = new CodeInstruction(cm.Instruction);
			cm.Advance(2);
			var branch = new CodeInstruction(OpCodes.Brtrue, cm.Operand);
			cm.Advance(1)
				.InsertAndAdvance(loadEntity)
				.InsertAndAdvance(isExtrapolated)
				.InsertAndAdvance(branch);


			return cm.InstructionEnumeration();
		}
	}
}
