using PhantomBrigade;
using PhantomBrigade.Data;
using PBDataHelperStats = PhantomBrigade.Data.DataHelperStats;

using UnityEngine;

namespace EchKode.PBMods.WeaponCooldown
{
	public static class DataHelperStats
	{
		public static (bool, float) TryGetActivationLockoutDuration(EquipmentEntity part, DataBlockActionCore dataCore)
		{
			if (dataCore.durationType == DurationType.Variable)
			{
				return (false, 0f);
			}

			if (part == null)
			{
				return (false, 0f);
			}
			if (part.isWrecked || !part.isFunctional)
			{
				Debug.LogWarningFormat(
					"Mod {0} ({1}) TryGetActivationLockoutDuration() shouldn't be looking for activation lockout duration on a wrecked/non-functional part | ID: E-{2} | status: {3}",
					ModLink.modIndex,
					ModLink.modID,
					part.id.id,
					part.isWrecked
						? "wrecked"
						: "non-functional");
				return (false, 0f);
			}
			if (!part.hasPrimaryActivationSubsystem)
			{
				return (false, 0f);
			}

			var subsystem = IDUtility.GetEquipmentEntity(part.primaryActivationSubsystem.equipmentID);
			if (subsystem == null)
			{
				return (false, 0f);
			}
			if (!subsystem.hasDataLinkSubsystem)
			{
				return (false, 0f);
			}

			var data = subsystem.dataLinkSubsystem.data;
			if (data == null)
			{
				return (false, 0f);
			}

			if (!data.TryGetFloat(statName, out var lockoutDuration))
			{
				if (LoggingToggles.ActivationLockoutStat)
				{
					Debug.LogFormat(
						"Mod {0} ({1}) didn't find stat on part | ID: E-{2} | subsystem: E-{3} ({4}) | stat name: {5}",
						ModLink.modIndex,
						ModLink.modID,
						part.id.id,
						subsystem.id.id,
						subsystem.dataKeySubsystem.s,
						statName);
					if (data.customProcessed?.floats != null && data.customProcessed.floats.Count != 0)
					{
						Debug.LogFormat(
							"Mod {0} ({1}) part has custom floats | ID: E-{2} | subsystem: E-{3} ({4}) | custom floats ({5}): {6}",
							ModLink.modIndex,
							ModLink.modID,
							part.id.id,
							subsystem.id.id,
							subsystem.dataKeySubsystem.s,
							data.customProcessed.floats.Count,
							data.customProcessed.floats.ToStringFormattedKeyValuePairs());
					}
				}
				return (false, 0f);
			}

			if (lockoutDuration < 0f || lockoutDuration.RoughlyEqual(0f))
			{
				if (LoggingToggles.ActivationLockoutStat)
				{
					Debug.LogFormat(
						"Mod {0} ({1}) found unusable activation lockout duration: should be a positive non-zero number | subsystem: E-{2} ({3}) | duration: {4:F3}",
						ModLink.modIndex,
						ModLink.modID,
						subsystem.id.id,
						subsystem.dataKeySubsystem.s,
						lockoutDuration);
				}
				return (false, 0f);
			}

			if (data.TryGetInt(valueTypeName, out var valueType) && valueType == (int)ValueType.Percentage)
			{
				if (lockoutDuration > 10f)
				{
					// Not going to guess what the user intended here but most probable is that a whole number
					// was entered as the percentage instead of a value in the range 0-1.

					if (LoggingToggles.ActivationLockoutStat)
					{
						Debug.LogFormat(
							"Mod {0} ({1}) found unusable percentage activation lockout duration: should be less than 1000% | subsystem: E-{2} ({3}) | percentage: {4:#.0%}",
							ModLink.modIndex,
							ModLink.modID,
							subsystem.id.id,
							subsystem.dataKeySubsystem.s,
							lockoutDuration);
					}
					return (false, 0f);
				}

				var activationDuration = dataCore.durationType == DurationType.Equipment
					? PBDataHelperStats.GetCachedStatForPart(UnitStats.activationDuration, part)
					: dataCore.duration;
				if (LoggingToggles.ActivationLockoutStat)
				{
					Debug.LogFormat(
						"Mod {0} ({1}) found percentage activation lockout duration | subsystem: E-{2} ({3}) | action duration: {4:F3}s ({5}) | percentage: {6:#.0%}",
						ModLink.modIndex,
						ModLink.modID,
						subsystem.id.id,
						subsystem.dataKeySubsystem.s,
						activationDuration,
						dataCore.durationType == DurationType.Equipment
							? "act_duration"
							: "dataCore.duration",
						lockoutDuration);
				}
				lockoutDuration *= activationDuration;
			}

			if (LoggingToggles.ActivationLockoutStat)
			{
				Debug.LogFormat(
					"Mod {0} ({1}) found activation lockout duration | subsystem: E-{2} ({3}) | duration: {4:F3}s | value type: {5}",
					ModLink.modIndex,
					ModLink.modID,
					subsystem.id.id,
					subsystem.dataKeySubsystem.s,
					lockoutDuration,
					valueType);
			}

			return (true, lockoutDuration);
		}

		private enum ValueType
		{
			Constant = 0,
			Percentage = 1,
		}

		const string statName = "act_lockout_duration";
		const string valueTypeName = "act_lockout_duration_value_type";
	}
}
