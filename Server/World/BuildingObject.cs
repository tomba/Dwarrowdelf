﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dwarrowdelf.Server
{
	public abstract class LargeGameObject : BaseGameObject, ILargeGameObject
	{
		[SaveGameProperty]
		public Environment Environment { get; private set; }
		IEnvironment ILargeGameObject.Environment { get { return this.Environment as IEnvironment; } }

		[SaveGameProperty]
		public IntRectZ Area { get; private set; }

		protected LargeGameObject(ObjectType objectType, IntRectZ area)
			: base(objectType)
		{
			this.Area = area;
		}

		protected LargeGameObject(SaveGameContext ctx, ObjectType objectType)
			: base(ctx, objectType)
		{
		}

		public bool Contains(IntPoint3D point)
		{
			return this.Area.Contains(point);
		}

		protected void SetEnvironment(Environment env)
		{
			if (this.Environment != null)
				this.Environment.RemoveLargeObject(this);

			this.Environment = env;

			if (this.Environment != null)
				this.Environment.AddLargeObject(this);
		}
	}

	[SaveGameObject(UseRef = true)]
	public class BuildingObject : LargeGameObject, IBuildingObject
	{
		internal static BuildingObject Create(World world, Environment env, BuildingObjectBuilder builder)
		{
			var ob = new BuildingObject(builder);
			ob.Initialize(world, env);
			return ob;
		}

		[SaveGameProperty]
		public BuildingID BuildingID { get; private set; }
		public BuildingInfo BuildingInfo { get { return Buildings.GetBuildingInfo(this.BuildingID); } }

		BuildingObject(BuildingObjectBuilder builder)
			: base(ObjectType.Building, builder.Area)
		{
			this.BuildingID = builder.BuildingID;
			this.BuildingState = BuildingState.NeedsCleaning;
		}

		BuildingObject(SaveGameContext ctx)
			: base(ctx, ObjectType.Building)
		{
		}

		[OnSaveGameDeserialized]
		void OnDeserialized()
		{
			this.World.TickStarting += OnWorldTickStarting;
		}

		protected override void Initialize(World world)
		{
			throw new NotImplementedException();
		}

		void Initialize(World world, Environment env)
		{
			if (BuildingObject.VerifyBuildSite(env, this.Area) == false)
				throw new Exception();

			SetEnvironment(env);
			base.Initialize(world);
			CheckState();
			this.World.TickStarting += OnWorldTickStarting;
		}

		void OnWorldTickStarting()
		{
			// XXX
			CheckState();
		}

		public override void Destruct()
		{
			SetEnvironment(null);
			base.Destruct();
		}

		protected override void SerializeTo(BaseGameObjectData data, ObjectVisibility visibility)
		{
			base.SerializeTo(data, visibility);

			SerializeToInternal((BuildingData)data, visibility);
		}

		void SerializeToInternal(BuildingData data, ObjectVisibility visibility)
		{
			data.ID = this.BuildingInfo.ID;
			data.Area = this.Area;
			data.Environment = this.Environment.ObjectID;
			data.State = this.BuildingState;
		}

		public override void SendTo(IPlayer player, ObjectVisibility visibility)
		{
			var data = new BuildingData();

			SerializeTo(data, visibility);

			player.Send(new Messages.ObjectDataMessage() { ObjectData = data });
		}

		protected override Dictionary<PropertyID, object> SerializeProperties(ObjectVisibility visibility)
		{
			var props = base.SerializeProperties(visibility);
			props[PropertyID.BuildingState] = m_state;
			return props;
		}

		[SaveGameProperty]
		BuildingState m_state;
		public BuildingState BuildingState
		{
			get { return m_state; }
			set { if (m_state == value) return; m_state = value; NotifyInt(PropertyID.BuildingState, (int)value); }
		}

		void CheckState()
		{
			var env = this.Environment;
			BuildingState newState = BuildingState.Functional;

			if (this.Area.Range().Any(p => env.GetInteriorID(p) != InteriorID.Empty))
				newState = BuildingState.NeedsCleaning;

			if (newState != this.BuildingState)
				this.BuildingState = newState;
		}

		public bool VerifyBuildItem(Living builder, IEnumerable<ObjectID> sourceObjects, ItemID dstItemID)
		{
			if (!Contains(builder.Location))
				return false;

			var srcArray = sourceObjects.Select(oid => this.World.FindObject<ItemObject>(oid)).ToArray();

			if (srcArray.Any(o => o == null || !this.Contains(o.Location)))
				return false;

			switch (this.BuildingInfo.ID)
			{
				case BuildingID.Carpenter:
					if (srcArray[0].MaterialCategory != MaterialCategory.Wood)
						return false;
					break;

				case BuildingID.Mason:
					if (srcArray[0].MaterialCategory != MaterialCategory.Rock)
						return false;
					break;

				default:
					return false;
			}

			return this.BuildingInfo.ItemBuildableFrom(dstItemID, srcArray);
		}


		public bool PerformBuildItem(Living builder, IEnumerable<ObjectID> sourceObjects, ItemID dstItemID)
		{
			if (!VerifyBuildItem(builder, sourceObjects, dstItemID))
				return false;

			var obs = sourceObjects.Select(oid => this.World.FindObject<ItemObject>(oid));

			var itemBuilder = new ItemObjectBuilder(dstItemID, obs.First().MaterialID);
			var item = itemBuilder.Create(this.World);

			foreach (var ob in obs)
				ob.Destruct();

			if (item.MoveTo(builder.Environment, builder.Location) == false)
				throw new Exception();

			return true;
		}

		public static bool VerifyBuildSite(Environment env, IntRectZ area)
		{
			return area.Range().All(p => env.GetTerrainID(p) == TerrainID.NaturalFloor);
		}
	}

	public class BuildingObjectBuilder
	{
		public BuildingID BuildingID { get; private set; }
		public IntRectZ Area { get; private set; }

		public BuildingObjectBuilder(BuildingID id, IntRectZ area)
		{
			this.BuildingID = id;
			this.Area = area;
		}

		public BuildingObject Create(World world, Environment env)
		{
			return BuildingObject.Create(world, env, this);
		}
	}
}
