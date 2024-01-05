// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using PhantomBrigade;
using PhantomBrigade.AI.Components;

namespace EchKode.PBMods.WeaponCooldown
{
	public sealed class ExtendedPlannedEquipmentUseRecord : PlannedEquipmentUseRecord
	{
		public int m_partID = IDUtility.invalidID;
		public float m_activationLockoutDuration;
	}
}
