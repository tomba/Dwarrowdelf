﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace Dwarrowdelf.Jobs.Assignments
{
	[SaveGameObjectByRef]
	public sealed class MineAssignment : Assignment
	{
		[SaveGameProperty]
		readonly IntPoint3D m_location;
		[SaveGameProperty]
		readonly MineActionType m_mineActionType;
		[SaveGameProperty]
		readonly IEnvironmentObject m_environment;

		public MineAssignment(IJobObserver parent, IEnvironmentObject environment, IntPoint3D location, MineActionType mineActionType)
			: base(parent)
		{
			m_environment = environment;
			m_location = location;
			m_mineActionType = mineActionType;
		}

		MineAssignment(SaveGameContext ctx)
			: base(ctx)
		{
		}

		protected override GameAction PrepareNextActionOverride(out JobStatus progress)
		{
			var v = m_location - this.Worker.Location;

			if (!this.Worker.Location.IsAdjacentTo(m_location, DirectionSet.PlanarUpDown))
			{
				progress = JobStatus.Fail;
				return null;
			}

			var action = new MineAction(v.ToDirection(), m_mineActionType);
			progress = JobStatus.Ok;
			return action;
		}

		public override string ToString()
		{
			return "Mine";
		}
	}
}
