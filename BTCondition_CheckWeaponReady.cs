using Content.Code.Utility;

using PhantomBrigade;
using PhantomBrigade.AI.Components;
using PhantomBrigade.AI.BT;
using PhantomBrigade.Data;
using PBAIUtility = PhantomBrigade.AI.AIUtility;

using UnityEngine;

using YamlDotNet.Serialization;

namespace EchKode.PBMods.WeaponCooldown
{
	[TypeHinted]
	[System.Serializable]
	public sealed class BTCondition_CheckWeaponReady : BTNodeWithoutState
	{
		[YamlForce]
		private AIActionType m_actionType;

		[YamlForce]
		private bool m_value;

		public BTCondition_CheckWeaponReady() { }

		public BTCondition_CheckWeaponReady(AIActionType p_actionType, bool p_value)
		{
			m_actionType = p_actionType;
			m_value = p_value;
		}

		public override string DebugText => string.Format("CheckWeaponReady({0}, {1})", m_actionType, m_value);

		protected override BTStatus OnUpdate(AIEntity p_myEntity) 
		{
			if (LoggingToggles.BTUpdates)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) BTCondition_CheckWeaponReady.OnUpdate() called | agent ID: {2} | planning time: {3:F3}s",
					ModLink.modIndex,
					ModLink.modID,
					p_myEntity.id.id,
					PBAIUtility.PlanningTime());
			}

			if (!p_myEntity.hasPlannedEquipmentUse || p_myEntity.plannedEquipmentUse.steps.Count == 0)
			{
				return m_value ? BTStatus.Success : BTStatus.Failed;
			}

			var lastUseStep = p_myEntity.plannedEquipmentUse.steps[p_myEntity.plannedEquipmentUse.steps.Count - 1];
			var time = PBAIUtility.PlanningTime();
			if (time < lastUseStep.m_startTime)
			{
				Debug.LogErrorFormat(
					"Mod {0} ({1}) BTCondition_CheckWeaponReady.OnUpdate(): actions shouldn't be planned into the future | planning time: {2:F3}s | start time: {3:F3}s",
					ModLink.modIndex,
					ModLink.modID,
					time,
					lastUseStep.m_startTime);
				return BTStatus.Error;
			}
			if (time < lastUseStep.EndTime)
			{
				return m_value ? BTStatus.Failed : BTStatus.Success;
			}

			if (lastUseStep is ExtendedPlannedEquipmentUseRecord extended)
			{
				var actionFromTable = PBAIUtility.GetActionFromTable(p_myEntity, m_actionType);
				if (string.IsNullOrEmpty(actionFromTable))
				{
					Debug.LogErrorFormat(
						"Mod {0} ({1}) BTCondition_CheckWeaponReady.OnUpdate(): action type should be in table | AI action type: {2}",
						ModLink.modIndex,
						ModLink.modID,
						m_actionType);
					return BTStatus.Error;
				}

				var actionData = DataMultiLinker<DataContainerAction>.GetEntry(actionFromTable, false);
				if (actionData == null)
				{
					Debug.LogErrorFormat(
						"Mod {0} ({1}) BTCondition_CheckWeaponReady.OnUpdate(): action key from table should resolve to an action | action: {2} | AI action type: {3}",
						ModLink.modIndex,
						ModLink.modID,
						actionData.key,
						m_actionType);
					return BTStatus.Error;
				}

				var dataCore = actionData.dataCore;
				if (dataCore.durationType == DurationType.Variable)
				{
					Debug.LogErrorFormat(
						"Mod {0} ({1}) BTCondition_CheckWeaponReady.OnUpdate(): weapons shouldn't be used with actions of variable duration | action: {2} | AI action type: {3}",
						ModLink.modIndex,
						ModLink.modID,
						actionData.key,
						m_actionType);
					return BTStatus.Error;
				}

				if (!p_myEntity.hasContextLinkPersistent)
				{
					Debug.LogErrorFormat(
						"Mod {0} ({1}) BTCondition_CheckWeaponReady.OnUpdate(): agent should have a link to a unit | agent ID: {2}",
						ModLink.modIndex,
						ModLink.modID,
						p_myEntity.id.id);
					return BTStatus.Error;
				}

				var unit = IDUtility.GetPersistentEntity(p_myEntity.contextLinkPersistent.id);
				if (unit == null)
				{
					Debug.LogErrorFormat(
						"Mod {0} ({1}) BTCondition_CheckWeaponReady.OnUpdate(): agent should be able to resolve linked unit | agent ID: {2} | unit ID: {3}",
						ModLink.modIndex,
						ModLink.modID,
						p_myEntity.id.id,
						p_myEntity.contextLinkPersistent.id);
					return BTStatus.Error;
				}

				var (partOK, part) = DataHelperAction.TryGetEquipmentPart(unit, actionData);
				if (!partOK)
				{
					Debug.LogErrorFormat(
						"Mod {0} ({1}) BTCondition_CheckWeaponReady.OnUpdate(): shouldn't have planned actions for missing parts | agent ID: {2} | unit ID: {3} | action: {4}",
						ModLink.modIndex,
						ModLink.modID,
						p_myEntity.id.id,
						unit.id.id,
						actionData.key);
					return BTStatus.Error;
				}
				if (part.id.id != extended.m_partID)
				{
					return m_value ? BTStatus.Success : BTStatus.Failed;
				}

				if (time < extended.EndTime + extended.m_activationLockoutDuration)
				{
					return m_value ? BTStatus.Failed : BTStatus.Success;
				}
			}

			return m_value ? BTStatus.Success : BTStatus.Failed;
		}
	}
}
