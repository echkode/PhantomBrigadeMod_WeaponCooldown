using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;

namespace EchKode.PBMods.WeaponCooldown
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(PathUtility), nameof(PathUtility.TrimPastMovement))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Pu_TrimPastMovementTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Prevent creating new actions at turn start for a run action from previous turn if the duration of the
			// action is less than 0.25s. These runt runs are shorter than what the player is allowed to create and
			// don't properly work with the overlay sprites.

			var cm = new CodeMatcher(instructions, generator);
			var durationPropInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.duration));
			var durationMatch = new CodeMatch(OpCodes.Callvirt, durationPropInfo);
			var addMatch = new CodeMatch(OpCodes.Add);
			var subtract = new CodeInstruction(OpCodes.Sub);

			cm.MatchEndForward(durationMatch)
				.MatchEndForward(addMatch)
				.Advance(2)
				.InsertAndAdvance(subtract)
				.RemoveInstructions(2)
				.SetOperandAndAdvance(0.25f)
				.SetOpcodeAndAdvance(OpCodes.Blt);

			return cm.InstructionEnumeration();
		}
	}
}
