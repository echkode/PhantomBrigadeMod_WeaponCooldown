// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;

using PhantomBrigade;
using PhantomBrigade.AI.Components;
using PhantomBrigade.Data;
using PBAIUtility = PhantomBrigade.AI.AIUtility;

namespace EchKode.PBMods.WeaponCooldown
{
	public static class AIUtility
	{
		public static bool IsInLockout(AIEntity agent, AIActionType actionType)
		{
			if (!agent.hasPlannedEquipmentUse)
			{
				return false;
			}

			cachedRecords.Clear();
			foreach (var record in agent.plannedEquipmentUse.steps)
			{
				if (record is ExtendedPlannedEquipmentUseRecord extended)
				{
					cachedRecords.Add(extended);
				}
			}

			if (cachedRecords.Count == 0)
			{
				return false;
			}

			var actionFromTable = PBAIUtility.GetActionFromTable(agent, actionType);
			if (string.IsNullOrEmpty(actionFromTable))
			{
				return false;
			}

			var actionData = DataMultiLinker<DataContainerAction>.GetEntry(actionFromTable, false);
			if (actionData == null)
			{
				return false;
			}

			var dataCore = actionData.dataCore;
			if (dataCore.durationType == DurationType.Variable)
			{
				return false;
			}

			if (!agent.hasContextLinkPersistent)
			{
				return false;
			}

			var unit = IDUtility.GetPersistentEntity(agent.contextLinkPersistent.id);
			if (unit == null)
			{
				return false;
			}

			var (partOK, part) = DataHelperAction.TryGetEquipmentPart(unit, actionData);
			if (!partOK)
			{
				return false;
			}

			var startTime = PBAIUtility.PlanningTime();
			for (var i = cachedRecords.Count - 1; i >= 0; i -= 1)
			{
				var record = cachedRecords[i];
				if (part.id.id != record.m_partID)
				{
					continue;
				}
				var useEndTime = record.EndTime + record.m_activationLockoutDuration;
				if (record.m_startTime <= startTime && startTime < useEndTime)
				{
					return true;
				}
			}

			return false;
		}

		static readonly List<ExtendedPlannedEquipmentUseRecord> cachedRecords = new List<ExtendedPlannedEquipmentUseRecord>();
	}
}
