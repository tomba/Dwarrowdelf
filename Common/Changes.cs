﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Dwarrowdelf
{
	[Serializable]
	public abstract class Change
	{
	}

	[Serializable]
	public class TickStartChange : Change
	{
		public int TickNumber { get; private set; }

		public TickStartChange(int tickNumber)
		{
			this.TickNumber = tickNumber;
		}

		public override string ToString()
		{
			return String.Format("TickStartChange({0})", this.TickNumber);
		}
	}

	[Serializable]
	public class TurnStartSimultaneousChange : Change
	{
		public TurnStartSimultaneousChange()
		{
		}
	}

	[Serializable]
	public class TurnEndSimultaneousChange : Change
	{
		public TurnEndSimultaneousChange()
		{
		}
	}

	[Serializable]
	public class TurnStartSequentialChange : Change
	{
		[NonSerialized]
		ILiving m_living;
		ObjectID m_livingID;

		public ILiving Living { get { return m_living; } }
		public ObjectID LivingID { get { return m_livingID; } }

		public TurnStartSequentialChange(ILiving living)
		{
			m_living = living;
			m_livingID = living.ObjectID;
		}
	}

	[Serializable]
	public class TurnEndSequentialChange : Change
	{
		[NonSerialized]
		ILiving m_living;
		ObjectID m_livingID;

		public ILiving Living { get { return m_living; } }
		public ObjectID LivingID { get { return m_livingID; } }

		public TurnEndSequentialChange(ILiving living)
		{
			m_living = living;
			m_livingID = living.ObjectID;
		}
	}

	[Serializable]
	public abstract class EnvironmentChange : Change
	{
		[NonSerialized]
		IEnvironment m_environment;
		ObjectID m_environmentID;

		public IEnvironment Environment { get { return m_environment; } }
		public ObjectID EnvironmentID { get { return m_environmentID; } }

		public EnvironmentChange(IEnvironment env)
		{
			m_environment = env;
			m_environmentID = env.ObjectID;
		}
	}

	[Serializable]
	public class MapChange : EnvironmentChange
	{
		public IntPoint3D Location { get; private set; }
		public TileData TileData { get; private set; }

		public MapChange(IEnvironment map, IntPoint3D l, TileData tileData)
			: base(map)
		{
			this.Location = l;
			this.TileData = tileData;
		}

		public override string ToString()
		{
			return String.Format("MapChange {0}, {1}, {2}", this.EnvironmentID, this.Location, this.TileData);
		}
	}

	[Serializable]
	public abstract class ObjectChange : Change
	{
		[NonSerialized]
		IBaseGameObject m_object;
		ObjectID m_objectID;

		public IBaseGameObject Object { get { return m_object; } }
		public ObjectID ObjectID { get { return m_objectID; } }

		protected ObjectChange(IBaseGameObject @object)
		{
			m_object = @object;
			m_objectID = m_object.ObjectID;
		}
	}

	[Serializable]
	public class ObjectCreatedChange : ObjectChange
	{
		public ObjectCreatedChange(IBaseGameObject ob)
			: base(ob)
		{
		}

		public override string ToString()
		{
			return String.Format("ObjectCreatedChange({0})", this.ObjectID);
		}
	}

	[Serializable]
	public class ObjectDestructedChange : ObjectChange
	{
		public ObjectDestructedChange(IBaseGameObject ob)
			: base(ob)
		{
		}

		public override string ToString()
		{
			return String.Format("ObjectDestructedChange({0})", this.ObjectID);
		}
	}

	[Serializable]
	public class ObjectMoveChange : ObjectChange
	{
		[NonSerialized]
		IGameObject m_source;
		ObjectID m_sourceID;
		IntPoint3D m_sourceLocation;
		[NonSerialized]
		IGameObject m_destination;
		ObjectID m_destinationID;
		IntPoint3D m_destinationLocation;

		public IGameObject Source { get { return m_source; } }
		public ObjectID SourceMapID { get { return m_sourceID; } }
		public IntPoint3D SourceLocation { get { return m_sourceLocation; } }

		public IGameObject Destination { get { return m_destination; } }
		public ObjectID DestinationMapID { get { return m_destinationID; } }
		public IntPoint3D DestinationLocation { get { return m_destinationLocation; } }

		public ObjectMoveChange(IGameObject mover, IGameObject sourceEnv, IntPoint3D sourceLocation,
			IGameObject destinationEnv, IntPoint3D destinationLocation)
			: base(mover)
		{
			m_source = sourceEnv;
			if (m_source != null)
				m_sourceID = m_source.ObjectID;
			m_sourceLocation = sourceLocation;
			m_destination = destinationEnv;
			if (m_destination != null)
				m_destinationID = m_destination.ObjectID;
			m_destinationLocation = destinationLocation;
		}

		public override string ToString()
		{
			return String.Format("ObjectMoveChange {0} ({1}, {2}) -> ({3}, {4})",
				this.ObjectID, this.SourceMapID, this.SourceLocation, this.DestinationMapID, this.DestinationLocation);
		}
	}

	[Serializable]
	public class ObjectMoveLocationChange : ObjectChange
	{
		public IntPoint3D SourceLocation;
		public IntPoint3D DestinationLocation;

		public ObjectMoveLocationChange(IGameObject mover, IntPoint3D sourceLocation, IntPoint3D destinationLocation)
			: base(mover)
		{
			this.SourceLocation = sourceLocation;
			this.DestinationLocation = destinationLocation;
		}

		public override string ToString()
		{
			return String.Format("ObjectMoveLocationChange {0} {1} -> {2}",
				this.ObjectID, this.SourceLocation, this.DestinationLocation);
		}
	}

	[Serializable]
	public class FullObjectChange : ObjectChange
	{
		public FullObjectChange(IBaseGameObject ob)
			: base(ob)
		{
		}

		public BaseGameObjectData ObjectData { get; set; }

		public override string ToString()
		{
			return String.Format("FullObjectChange({0})", this.ObjectID);
		}
	}

	[Serializable]
	public abstract class PropertyChange : ObjectChange
	{
		public PropertyID PropertyID { get; private set; }

		protected PropertyChange(IBaseGameObject ob, PropertyID propertyID)
			: base(ob)
		{
			this.PropertyID = propertyID;
		}
	}

	[Serializable]
	public class PropertyObjectChange : PropertyChange
	{
		public object Value { get; private set; }

		public PropertyObjectChange(IBaseGameObject ob, PropertyID propertyID, object value)
			: base(ob, propertyID)
		{
			this.Value = value;
		}

		public override string ToString()
		{
			return String.Format("PropertyChange({0}, {1} : {2})", this.ObjectID, this.PropertyID, this.Value);
		}
	}

	[Serializable]
	public class PropertyIntChange : PropertyChange
	{
		public int Value { get; private set; }

		public PropertyIntChange(IBaseGameObject ob, PropertyID propertyID, int value)
			: base(ob, propertyID)
		{
			this.Value = value;
		}

		public override string ToString()
		{
			return String.Format("PropertyIntChange({0}, {1} : {2})", this.ObjectID, this.PropertyID, this.Value);
		}
	}

	[Serializable]
	public class SkillChange : ObjectChange
	{
		public SkillID SkillID { get; private set; }
		public byte Level { get; private set; }

		public SkillChange(IBaseGameObject ob, SkillID skillID, byte level)
			: base(ob)
		{
			this.SkillID = skillID;
			this.Level = level;
		}

		public override string ToString()
		{
			return String.Format("SkillChange({0}, {1} : {2})", this.ObjectID, this.SkillID, this.Level);
		}
	}

	[Serializable]
	public class ActionStartedChange : ObjectChange
	{
		public GameAction Action { get; set; }
		public ActionPriority Priority { get; set; }
		public int UserID { get; set; }

		public ActionStartedChange(IBaseGameObject ob)
			: base(ob)
		{
		}

		public override string ToString()
		{
			return String.Format("ActionStartedChange({0}, {1}, uid: {2})",
				this.ObjectID, this.Action, this.UserID);
		}
	}

	[Serializable]
	public class ActionProgressChange : ObjectChange
	{
		// just for debug
		public GameAction ActionXXX { get; set; }
		public int UserID { get; set; }
		public int TotalTicks { get; set; }
		public int TicksUsed { get; set; }

		public ActionProgressChange(IBaseGameObject ob)
			: base(ob)
		{
		}

		public override string ToString()
		{
			return String.Format("ActionProgressChange(UID({0}), {1}, ticks: {2}/{3})",
				this.UserID, this.ObjectID, this.TicksUsed, this.TotalTicks);
		}
	}

	[Serializable]
	public class ActionDoneChange : ObjectChange
	{
		// just for debug
		public GameAction ActionXXX { get; set; }
		public int UserID { get; set; }
		public ActionState State { get; set; }
		public string Error { get; set; }

		public ActionDoneChange(IBaseGameObject ob)
			: base(ob)
		{
		}

		public override string ToString()
		{
			return String.Format("ActionDoneChange(UID({0}), {1}, state: {2}, error: {3})",
				this.UserID, this.ObjectID, this.State, this.Error ?? "<none>");
		}
	}

	public enum DamageCategory
	{
		None,
		Melee,
	}

	[Serializable]
	public class DamageChange : ObjectChange
	{
		[NonSerialized]
		IBaseGameObject m_attacker;
		ObjectID m_attackerID;

		public IBaseGameObject Attacker { get { return m_attacker; } }
		public ObjectID AttackerID { get { return m_attackerID; } }

		public DamageCategory DamageCategory;
		public int Damage;

		public bool IsHit;

		public DamageChange(IBaseGameObject target, IBaseGameObject attacker, DamageCategory cat, int damage)
			: base(target)
		{
			m_attacker = attacker;
			m_attackerID = attacker.ObjectID;

			this.DamageCategory = cat;
			this.Damage = damage;
		}
	}

	[Serializable]
	public class DeathChange : ObjectChange
	{
		public DeathChange(IBaseGameObject ob)
			: base(ob)
		{
		}
	}
}
