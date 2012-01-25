﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dwarrowdelf
{
	public interface IWorld
	{
		int TickNumber { get; }
		event Action TickStarting;
		Random Random { get; }
	}

	public interface IIdentifiable
	{
		ObjectID ObjectID { get; }
		ObjectType ObjectType { get; }
	}

	public interface IBaseObject : IIdentifiable
	{
		IWorld World { get; }
		bool IsDestructed { get; }
		event Action<IBaseObject> Destructed;
	}

	public interface IAreaObject : IBaseObject
	{
		IEnvironmentObject Environment { get; }
		IntRectZ Area { get; }
	}

	public interface IBuildingObject : IAreaObject
	{
		BuildingInfo BuildingInfo { get; }
	}

	public interface IContainerObject : IBaseObject
	{
		IEnumerable<IMovableObject> Inventory { get; }
	}

	public interface IEnvironmentObject : IContainerObject, AStar.IAStarEnvironment
	{
		VisibilityMode VisibilityMode { get; }

		IntCuboid Bounds { get; }
		bool Contains(IntPoint3 p);

		TerrainID GetTerrainID(IntPoint3 l);
		MaterialID GetTerrainMaterialID(IntPoint3 l);

		InteriorID GetInteriorID(IntPoint3 l);
		MaterialID GetInteriorMaterialID(IntPoint3 l);

		TerrainInfo GetTerrain(IntPoint3 l);
		MaterialInfo GetTerrainMaterial(IntPoint3 l);

		InteriorInfo GetInterior(IntPoint3 l);
		MaterialInfo GetInteriorMaterial(IntPoint3 l);

		TileData GetTileData(IntPoint3 l);

		bool GetTileFlags(IntPoint3 l, TileFlags flags);

		IEnumerable<IMovableObject> GetContents(IntPoint3 pos);
		IEnumerable<IMovableObject> GetContents(IntRectZ rect);
	}

	public interface IMovableObject : IContainerObject
	{
		/// <summary>
		/// Return Parent as IEnvironmentObject
		/// </summary>
		IEnvironmentObject Environment { get; }
		IContainerObject Parent { get; }
		IntPoint3 Location { get; }
	}

	public interface IConcreteObject : IMovableObject
	{
		string Name { get; }
		GameColor Color { get; }
		MaterialInfo Material { get; }
		MaterialCategory MaterialCategory { get; }
		MaterialID MaterialID { get; }
	}

	public interface ILivingObject : IConcreteObject
	{
		GameAction CurrentAction { get; }
		ActionPriority ActionPriority { get; }
		bool HasAction { get; }

		LivingID LivingID { get; }
		LivingCategory LivingCategory { get; }

		int VisionRange { get; }

		byte GetSkillLevel(SkillID skill);

		int FoodFullness { get; }
		int WaterFullness { get; }
	}

	public interface IItemObject : IConcreteObject
	{
		ItemInfo ItemInfo { get; }
		ItemCategory ItemCategory { get; }
		ItemID ItemID { get; }

		object ReservedBy { get; set; }
		bool IsReserved { get; }

		bool IsArmor { get; }
		bool IsWeapon { get; }

		ArmorInfo ArmorInfo { get; }
		WeaponInfo WeaponInfo { get; }

		ILivingObject Wearer { get; }
		ILivingObject Wielder { get; }

		bool IsWorn { get; }
		bool IsWielded { get; }

		int NutritionalValue { get; }
		int RefreshmentValue { get; }
	}

	public enum LivingVisionMode
	{
		/// <summary>
		/// everything inside VisionRange is visible
		/// </summary>
		SquareFOV,

		/// <summary>
		/// use LOS algorithm
		/// </summary>
		LOS,
	}

	public enum VisibilityMode
	{
		/// <summary>
		/// Everything visible
		/// </summary>
		AllVisible,

		/// <summary>
		/// Areas inside the mountain are not visible
		/// </summary>
		GlobalFOV,

		/// <summary>
		/// Things seen by controllables are visible
		/// </summary>
		LivingLOS,
	}
}
