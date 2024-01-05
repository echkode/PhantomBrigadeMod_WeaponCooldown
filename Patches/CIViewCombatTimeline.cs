using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;
using PhantomBrigade.Data;
using PBCIViewCombatTimeline = CIViewCombatTimeline;
using PBCombatUIUtility = PhantomBrigade.CombatUIUtility;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	using Float3 = System.ValueTuple<float, float, float>;

	static partial class Patch
	{
		[HarmonyPatch(typeof(PBCIViewCombatTimeline), "AdjustTimelineRegions")]
		[HarmonyPrefix]
		static bool Civct_AdjustTimelineRegions(
			int actionIDSelected,
			float actionStartTime,
			float offsetSelected,
			bool offsetCorrectionAllowed,
			int depth,
			PBCIViewCombatTimeline __instance)
		{
			if (!offsetCorrectionAllowed)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) AdjustTimelineRegions called with unexpected argument value, reverting to original implementation | offsetCorrectionAllowed: false",
					ModLink.modIndex,
					ModLink.modID);
				return true;
			}

			if (depth != 0)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) AdjustTimelineRegions called with unexpected argument value, reverting to original implementation | depth: {2}",
					ModLink.modIndex,
					ModLink.modID,
					depth);
				return true;
			}

			CIViewCombatTimeline.AdjustTimelineRegions(
				__instance,
				actionIDSelected,
				actionStartTime,
				offsetSelected);
			return false;
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), nameof(PBCIViewCombatTimeline.ConfigureActionPainted))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_ConfigureActionPaintedTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Add call to configure background sprites so that the warning background sprite can be repurposed
			// to show the activation lockout period.

			var cm = new CodeMatcher(instructions, generator);
			var spriteBackgroundOverlapFieldInfo = AccessTools.DeclaredField(typeof(CIHelperTimelineAction), nameof(CIHelperTimelineAction.spriteBackgroundOverlap));
			var setActiveMethodInfo = AccessTools.DeclaredMethod(typeof(GameObject), nameof(GameObject.SetActive));
			var dataCoreFieldInfo = AccessTools.DeclaredField(typeof(DataContainerAction), nameof(DataContainerAction.dataCore));
			var actionColorFieldInfo = AccessTools.DeclaredField(typeof(DataBlockActionUI), nameof(DataBlockActionUI.color));
			var roundToIntMethodInfo = AccessTools.DeclaredMethod(typeof(Mathf), nameof(Mathf.RoundToInt));
			var setDimensionsMethodInfo = AccessTools.DeclaredMethod(typeof(UIWidget), nameof(UIWidget.SetDimensions));
			var setLocalPositionMethodInfo = AccessTools.DeclaredPropertySetter(typeof(Transform), nameof(Transform.localPosition));
			var spriteBackgroundOverlapMatch = new CodeMatch(OpCodes.Ldfld, spriteBackgroundOverlapFieldInfo);
			var setActiveMatch = new CodeMatch(OpCodes.Callvirt, setActiveMethodInfo);
			var dataCoreMatch = new CodeMatch(OpCodes.Ldfld, dataCoreFieldInfo);
			var actionColorMatch = new CodeMatch(OpCodes.Ldfld, actionColorFieldInfo);
			var roundToIntMatch = new CodeMatch(OpCodes.Call, roundToIntMethodInfo);
			var setDimensionsMatch = new CodeMatch(OpCodes.Callvirt, setDimensionsMethodInfo);
			var setLocalPositionMatch = new CodeMatch(OpCodes.Callvirt, setLocalPositionMethodInfo);
			var loadTimeline = new CodeInstruction(OpCodes.Ldarg_0);
			var loadHelpersActionsPlanned = CodeInstruction.LoadField(typeof(PBCIViewCombatTimeline), "helpersActionsPlanned");
			var loadSelectedUnitID = CodeInstruction.LoadField(typeof(PBCIViewCombatTimeline), "unitPersistentIDSelectedLast");
			var loadHelper = new CodeInstruction(OpCodes.Ldarg_1);
			var loadStartTime = new CodeInstruction(OpCodes.Ldarg_2);
			var sub = new CodeInstruction(OpCodes.Sub);
			var configureActionBackground = CodeInstruction.Call(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.ConfigureActionPaintedBackground));

			cm.MatchEndForward(spriteBackgroundOverlapMatch)
				.Advance(2);
			var loadOverlap = cm.Instruction.Clone();

			cm.MatchStartForward(roundToIntMatch)
				.MatchEndBackwards(setActiveMatch)
				.Advance(1);
			var loadOverlapStartTime = cm.Instruction.Clone();

			cm.Start()
				.MatchStartForward(dataCoreMatch)
				.Advance(-1);
			var loadActionData = cm.Instruction.Clone();

			cm.MatchEndForward(actionColorMatch)
				.Advance(1);
			var loadColor = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);

			cm.MatchEndForward(roundToIntMatch)
				.Advance(1);
			var storeWidth = cm.Instruction.Clone();
			var loadWidth = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);

			cm.MatchStartForward(setDimensionsMatch)
				.MatchEndBackwards(setLocalPositionMatch)
				.Advance(1)
				.InsertAndAdvance(loadTimeline)
				.InsertAndAdvance(loadHelpersActionsPlanned)
				.InsertAndAdvance(loadTimeline)
				.InsertAndAdvance(loadSelectedUnitID)
				.InsertAndAdvance(loadActionData)
				.InsertAndAdvance(loadHelper)
				.InsertAndAdvance(loadOverlap)
				.InsertAndAdvance(loadStartTime)
				.InsertAndAdvance(loadOverlapStartTime)
				.InsertAndAdvance(sub)
				.InsertAndAdvance(loadColor)
				.InsertAndAdvance(loadWidth)
				.InsertAndAdvance(configureActionBackground)
				.InsertAndAdvance(storeWidth);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), nameof(PBCIViewCombatTimeline.ConfigureActionPlanned))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_ConfigureActionPlannedTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Add call to configure background sprites so that the warning background sprite can be repurposed
			// to show the activation lockout period. This is complicated because the warning sprite is also
			// used for locked actions.
			//
			// !!! Assumes that a locked action will not have activation lockout.

			var cm = new CodeMatcher(instructions, generator);
			var lockedLocal = generator.DeclareLocal(typeof(bool));
			var colorFieldInfo = AccessTools.DeclaredField(typeof(DataBlockActionUI), nameof(DataBlockActionUI.color));
			var roundToIntMethodInfo = AccessTools.DeclaredMethod(typeof(Mathf), nameof(Mathf.RoundToInt));
			var setDimensionsMethodInfo = AccessTools.DeclaredMethod(typeof(UIWidget), nameof(UIWidget.SetDimensions));
			var setLocalPositionMethodInfo = AccessTools.DeclaredPropertySetter(typeof(Transform), nameof(Transform.localPosition));
			var spriteBackgroundFieldInfo = AccessTools.DeclaredField(typeof(CIHelperTimelineAction), nameof(CIHelperTimelineAction.spriteBackground));
			var spriteLockFieldInfo = AccessTools.DeclaredField(typeof(CIHelperTimelineAction), nameof(CIHelperTimelineAction.spriteLock));
			var spriteBackgroundWarningFieldInfo = AccessTools.DeclaredField(typeof(CIHelperTimelineAction), nameof(CIHelperTimelineAction.spriteBackgroundWarning));
			var colorMatch = new CodeMatch(OpCodes.Ldfld, colorFieldInfo);
			var roundToIntMatch = new CodeMatch(OpCodes.Call, roundToIntMethodInfo);
			var setDimensionsMatch = new CodeMatch(OpCodes.Callvirt, setDimensionsMethodInfo);
			var setLocalPositionMatch = new CodeMatch(OpCodes.Callvirt, setLocalPositionMethodInfo);
			var spriteBackgroundMatch = new CodeMatch(OpCodes.Ldfld, spriteBackgroundFieldInfo);
			var spriteLockMatch = new CodeMatch(OpCodes.Ldfld, spriteLockFieldInfo);
			var spriteBackgroundWarningMatch = new CodeMatch(OpCodes.Ldfld, spriteBackgroundWarningFieldInfo);
			var loadAction = new CodeInstruction(OpCodes.Ldloc_0);
			var loadHelper = new CodeInstruction(OpCodes.Ldarg_1);
			var configureActionBackground = CodeInstruction.Call(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.ConfigureActionBackground));
			var storeIsLocked = new CodeInstruction(OpCodes.Stloc_S, lockedLocal);
			var loadIsLocked = new CodeInstruction(OpCodes.Ldloc_S, lockedLocal);
			var dupe = new CodeInstruction(OpCodes.Dup);

			cm.MatchEndForward(colorMatch)
				.Advance(1);
			var loadColor = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);

			cm.MatchEndForward(roundToIntMatch)
				.Advance(1);
			var storeWidth = cm.Instruction.Clone();
			var loadWidth = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);

			// Insert call to configure background sprites.
			cm.MatchStartForward(setDimensionsMatch)
				.MatchEndBackwards(setLocalPositionMatch)
				.Advance(1)
				.InsertAndAdvance(loadAction)
				.InsertAndAdvance(loadHelper)
				.InsertAndAdvance(loadColor)
				.InsertAndAdvance(loadWidth)
				.InsertAndAdvance(configureActionBackground)
				.InsertAndAdvance(storeWidth);

			// Rearrange changes to spriteBackgroundWarning when the action is locked.
			cm.MatchEndForward(spriteBackgroundMatch)
				.Advance(1)
				.RemoveInstructions(2)
				.Advance(1)
				.RemoveInstructions(2);
			var storeColorField = cm.Instruction.Clone();
			cm.Advance(1)
				.RemoveInstructions(2)
				.MatchEndForward(spriteLockMatch)
				.Advance(2);
			var getIsLocked = cm.Instructions(2);
			cm.RemoveInstructions(2)
				.InsertAndAdvance(loadIsLocked)
				.MatchEndForward(spriteBackgroundWarningMatch)
				.Advance(2)
				.RemoveInstructions(2)
				.InsertAndAdvance(loadIsLocked)
				.Advance(1);
			cm.CreateLabel(out var skipLockedLabel);
			var skipLocked = new CodeInstruction(OpCodes.Brfalse_S, skipLockedLabel);
			cm.MatchStartBackwards(spriteLockMatch)
				.Advance(-1)
				.InsertAndAdvance(getIsLocked)
				.InsertAndAdvance(storeIsLocked);
			cm.MatchEndForward(spriteBackgroundWarningMatch)
				.Advance(-1)
				.InsertAndAdvance(loadIsLocked)
				.InsertAndAdvance(skipLocked)
				.Advance(2)
				.InsertAndAdvance(dupe)
				.Advance(3)
				.InsertAndAdvance(loadColor)
				.InsertAndAdvance(storeColorField);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), nameof(PBCIViewCombatTimeline.ConfigureActionPlanned))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_ConfigureActionPlannedTranspiler2(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Don't enable drag callback for locked actions.

			var cm = new CodeMatcher(instructions, generator);
			var isLockedMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.isLocked));
			var onActionDragMethodInfo = AccessTools.DeclaredMethod(typeof(PBCIViewCombatTimeline), "OnActionDrag");
			var isLockedMatch = new CodeMatch(OpCodes.Callvirt, isLockedMethodInfo);
			var onActionDragMatch = new CodeMatch(OpCodes.Ldftn, onActionDragMethodInfo);
			var ret = new CodeInstruction(OpCodes.Ret);

			cm.MatchStartForward(isLockedMatch)
				.Advance(-1);
			var isLocked = cm.Instructions(2);
			cm.MatchStartForward(onActionDragMatch)
				.Advance(-3);
			cm.CreateLabel(out var skipRetLabel);
			var skipRet = new CodeInstruction(OpCodes.Brfalse_S, skipRetLabel);
			cm.InsertAndAdvance(isLocked)
				.InsertAndAdvance(skipRet)
				.InsertAndAdvance(ret);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), nameof(PBCIViewCombatTimeline.ConfigureActionPlanned))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_ConfigureActionPlannedTranspiler3(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Fix widget and background warning sprite depths on timeline after configuring helper.

			var cm = new CodeMatcher(instructions, generator);
			var loadHelper = new CodeInstruction(OpCodes.Ldarg_1);
			var loadThis = new CodeInstruction(OpCodes.Ldarg_0);
			var loadPlannedHelpers = CodeInstruction.LoadField(typeof(PBCIViewCombatTimeline), "helpersActionsPlanned");
			var fixDepths = CodeInstruction.Call(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.FixActionWidgetDepths));

			cm.End();
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			cm.Insert(loadHelper)
				.AddLabels(labels)
				.Advance(1)
				.InsertAndAdvance(loadThis)
				.InsertAndAdvance(loadPlannedHelpers)
				.InsertAndAdvance(fixDepths);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), "OnActionDrag")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_OnActionDragTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Prevent dragging locked actions.

			var cm = new CodeMatcher(instructions, generator);
			var hasActionOwnerMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.hasActionOwner));
			var isLockedMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.isLocked));
			var hasActionOwnerMatch = new CodeMatch(OpCodes.Callvirt, hasActionOwnerMethodInfo);
			var isLocked = new CodeInstruction(OpCodes.Callvirt, isLockedMethodInfo);
			var ret = new CodeInstruction(OpCodes.Ret);

			cm.MatchStartForward(hasActionOwnerMatch)
				.Advance(-1);
			var loadAction = cm.Instruction.Clone();
			cm.CreateLabel(out var skipRetLabel);
			var skipRet = new CodeInstruction(OpCodes.Brfalse_S, skipRetLabel);
			cm.InsertAndAdvance(loadAction)
				.InsertAndAdvance(isLocked)
				.InsertAndAdvance(skipRet)
				.InsertAndAdvance(ret);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), nameof(PBCIViewCombatTimeline.OnActionSelected))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_OnActionSelectedTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var cm = new CodeMatcher(instructions, generator);
			var retMatch = new CodeMatch(OpCodes.Ret);
			var loadTimeline = CodeInstruction.LoadField(typeof(PBCIViewCombatTimeline), nameof(PBCIViewCombatTimeline.ins));
			var loadLastRemoveID = CodeInstruction.LoadField(typeof(PBCIViewCombatTimeline), "idOfActionToRemoveLast");
			var loadActionID = new CodeInstruction(OpCodes.Ldarg_0);
			var removeCancel = CodeInstruction.Call(typeof(PBCIViewCombatTimeline), "OnActionRemoveCancel");

			cm.MatchEndForward(retMatch)
				.Advance(1);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			cm.CreateLabel(out var skipCancelLabel);
			var skipCancel = new CodeInstruction(OpCodes.Beq_S, skipCancelLabel);
			cm.Insert(loadTimeline.Clone())
				.AddLabels(labels)
				.Advance(1)
				.InsertAndAdvance(loadLastRemoveID)
				.InsertAndAdvance(loadActionID)
				.InsertAndAdvance(skipCancel)
				.InsertAndAdvance(loadTimeline)
				.InsertAndAdvance(removeCancel);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), nameof(PBCIViewCombatTimeline.OnPaintedActionChange))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_OnPaintedActionChangeTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Replace inline overlap logic with a call to a function.

			var cm = new CodeMatcher(instructions, generator);
			var persistentEntityLocal = generator.DeclareLocal(typeof(PersistentEntity));
			var combatEntityLocal = generator.DeclareLocal(typeof(CombatEntity));
			var okLocal = generator.DeclareLocal(typeof(bool));
			var partLocal = generator.DeclareLocal(typeof(EquipmentEntity));
			var adjustedEndTimeLocal = generator.DeclareLocal(typeof(float));
			var isOverlappedMethodInfo = AccessTools.DeclaredMethod(typeof(PBCombatUIUtility), nameof(PBCombatUIUtility.IsIntervalOverlapped));
			var configurePaintedActionMethodInfo = AccessTools.DeclaredMethod(typeof(PBCIViewCombatTimeline), nameof(PBCIViewCombatTimeline.ConfigureActionPainted));
			var getPersistentEntityMethodInfo = AccessTools.DeclaredMethod(typeof(IDUtility), nameof(IDUtility.GetPersistentEntity), new System.Type[] { typeof(int) });
			var getIDMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatEntity), nameof(CombatEntity.id));
			var getActionEntityMethodInfo = AccessTools.DeclaredMethod(typeof(IDUtility), nameof(IDUtility.GetActionEntity));
			var isOverlappedMatch = new CodeMatch(OpCodes.Call, isOverlappedMethodInfo);
			var configurePaintedActionMatch = new CodeMatch(OpCodes.Call, configurePaintedActionMethodInfo);
			var storeStartTimeMatch = new CodeMatch(OpCodes.Starg_S);
			var getPersistentEntityMatch = new CodeMatch(OpCodes.Call, getPersistentEntityMethodInfo);
			var addMatch = new CodeMatch(OpCodes.Add);
			var getIDMatch = new CodeMatch(OpCodes.Callvirt, getIDMethodInfo);
			var getActionEntityMatch = new CodeMatch(OpCodes.Call, getActionEntityMethodInfo);
			var branchMatch = new CodeMatch(OpCodes.Brfalse_S);
			var loadFalse = new CodeInstruction(OpCodes.Ldc_I4_0);
			var storePersistentEntity = new CodeInstruction(OpCodes.Stloc_S, persistentEntityLocal);
			var loadPersistentEntity = new CodeInstruction(OpCodes.Ldloc_S, persistentEntityLocal);
			var storeCombatEntity = new CodeInstruction(OpCodes.Stloc_S, combatEntityLocal);
			var loadCombatEntity = new CodeInstruction(OpCodes.Ldloc_S, combatEntityLocal);
			var loadStartTime = new CodeInstruction(OpCodes.Ldarg_2);
			var loadDuration = new CodeInstruction(OpCodes.Ldarg_3);
			var loadEndTime = new CodeInstruction(OpCodes.Ldloc_2);
			var getOverlap = CodeInstruction.Call(typeof(CombatUIUtility), nameof(CombatUIUtility.GetOverlap));
			var dupe = new CodeInstruction(OpCodes.Dup);
			var loadStartTimeField = CodeInstruction.LoadField(typeof(Float3), nameof(Float3.Item1));
			var loadOverlapStartField = CodeInstruction.LoadField(typeof(Float3), nameof(Float3.Item2));
			var storeOverlapStart = new CodeInstruction(OpCodes.Stloc_3);
			var loadOverlapDurationField = CodeInstruction.LoadField(typeof(Float3), nameof(Float3.Item3));

			cm.MatchEndForward(getPersistentEntityMatch)
				.MatchEndForward(addMatch)
				.MatchStartForward(getIDMatch)
				.Advance(-1);
			var storeOverlapDuration = cm.Instruction.Clone();
			var storeOverlapDurationMatch = new CodeMatch(OpCodes.Stloc_S, cm.Operand);

			cm.MatchEndForward(isOverlappedMatch)
				.Advance(1);
			var storeOverlapFlag = cm.Instruction.Clone();

			cm.MatchStartForward(configurePaintedActionMatch)
				.MatchEndBackwards(storeOverlapDurationMatch)
				.Advance(1);
			var skipOverlap = new CodeInstruction(OpCodes.Brfalse, cm.Labels[0]);

			cm.MatchStartBackwards(storeStartTimeMatch);
			var storeStartTime = cm.Instruction.Clone();

			// Check to make sure we get the persistent entity and combat entity before entering
			// the overlap logic.
			cm.MatchStartBackwards(isOverlappedMatch)
				.MatchStartBackwards(getPersistentEntityMatch)
				.Advance(-1)
				.InsertAndAdvance(loadFalse)
				.InsertAndAdvance(storeOverlapFlag)
				.Advance(2)
				.InsertAndAdvance(storePersistentEntity)
				.InsertAndAdvance(loadPersistentEntity)
				.InsertAndAdvance(skipOverlap)
				.InsertAndAdvance(loadPersistentEntity)
				.Advance(1)
				.InsertAndAdvance(storeCombatEntity)
				.InsertAndAdvance(loadCombatEntity)
				.InsertAndAdvance(skipOverlap)
				.InsertAndAdvance(loadCombatEntity);

			cm.MatchEndForward(addMatch)
				.MatchEndForward(getIDMatch)
				.Advance(2);
			var loadActionData = cm.Instruction.Clone();

			// Stuff in call to get overlap start and duration.
			cm.MatchEndForward(getActionEntityMatch)
				.MatchEndForward(branchMatch)
				.Advance(1)
				.InsertAndAdvance(loadPersistentEntity)
				.InsertAndAdvance(loadActionData)
				.Advance(1)
				.InsertAndAdvance(loadStartTime)
				.InsertAndAdvance(loadDuration)
				.InsertAndAdvance(loadEndTime)
				.InsertAndAdvance(getOverlap)
				.InsertAndAdvance(dupe)
				.InsertAndAdvance(dupe)
				.InsertAndAdvance(loadStartTimeField)
				.InsertAndAdvance(storeStartTime)
				.InsertAndAdvance(loadOverlapStartField)
				.InsertAndAdvance(storeOverlapStart)
				.InsertAndAdvance(loadOverlapDurationField)
				.InsertAndAdvance(storeOverlapDuration);

			// Delete overlap logic (replaced by call above).
			var deleteStart = cm.Pos;
			cm.MatchStartForward(configurePaintedActionMatch)
				.MatchStartBackwards(storeOverlapDurationMatch);
			var offset = deleteStart - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), "OnTimelineClick")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_OnTimelineClickTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// If there is a pending action cancel, remove the cancel.

			var cm = new CodeMatcher(instructions, generator);
			var loadTimeline = new CodeInstruction(OpCodes.Ldarg_0);
			var removeCancel = CodeInstruction.Call(typeof(PBCIViewCombatTimeline), "OnActionRemoveCancel");

			cm.End()
				.InsertAndAdvance(loadTimeline)
				.InsertAndAdvance(removeCancel);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), "RefreshTimelineRegions")]
		[HarmonyPrefix]
		static bool Civct_RefreshTimelineRegionsPrefix(int actionIDSelected)
		{
			CIViewCombatTimeline.RefreshTimelineRegions(actionIDSelected);
			return false;
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), "UpdateScrubbing")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_UpdateScrubbingTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Cut out extraneous call to CombatUIUtility.IsIntervalOverlapped().

			var cm = new CodeMatcher(instructions, generator);
			var getDurationMethodInfo = AccessTools.DeclaredMethod(typeof(PBCombatUIUtility), nameof(PBCombatUIUtility.GetPaintedTimePlacementDuration));
			var getDurationMatch = new CodeMatch(OpCodes.Call, getDurationMethodInfo);
			var popMatch = new CodeMatch(OpCodes.Pop);

			cm.MatchEndForward(getDurationMatch)
				.Advance(2);
			var deletePos = cm.Pos;
			cm.MatchStartForward(popMatch);
			var offset = deletePos - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(PBCIViewCombatTimeline), "UpdateWarningTimeouts")]
		[HarmonyPostfix]
		static void Civct_UpdateWarningTimeoutsPostfix()
		{
			// The UILinkPainting patches may show the warning late toast so code needs to be added to
			// hide it when the painted action is canceled.

			var t = new Traverse(PBCIViewCombatTimeline.ins);
			if (t.Field<bool>("warningTimeoutLock").Value)
			{
				return;
			}

			var warningTimeoutField = t.Field<float>("warningTimeoutLate");
			if (warningTimeoutField.Value > 0f)
			{
				warningTimeoutField.Value -= TimeCustom.unscaledDeltaTime;
				PBCIViewCombatTimeline.ins.hideableWarningLate.SetVisible(true);
			}
			else
			{
				PBCIViewCombatTimeline.ins.hideableWarningLate.SetVisible(false);
			}
		}
	}
}
