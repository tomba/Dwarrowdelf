﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dwarrowdelf.Client
{
	// Stored in render tile data, needs to be short
	public enum SymbolID : short
	{
		Undefined,

		Unknown,

		// Empty symbol, e.g. to show only bg color
		Empty,

		// Tile that has not been visible
		Hidden,

		/* floors */
		Floor,
		Sand,

		/* interiors */
		Wall,
		ValuableOre,
		GemOre,
		StairsUp,
		StairsDown,
		StairsUpDown,

		SlopeUpNorth,
		SlopeUpNorthEast,
		SlopeUpEast,
		SlopeUpSouthEast,
		SlopeUpSouth,
		SlopeUpSouthWest,
		SlopeUpWest,
		SlopeUpNorthWest,

		ConiferousSapling,
		DeciduousSapling,
		ConiferousSapling2,
		DeciduousSapling2,
		ConiferousTree,
		ConiferousTree2,
		DeciduousTree,
		DeciduousTree2,
		DeadTree,
		Grass,
		Grass2,
		Grass3,
		Grass4,
		Shrub,

		/* Livings */
		Player,
		Sheep,
		Wolf,
		Dragon,
		Orc,

		/* items */
		UncutGem,
		Block,
		Bar,
		Gem,
		Key,
		Rock,
		Log,
		Contraption,
		Consumable,
		Chair,
		Table,
		Door,
		DoorClosed,
		Bed,
		Barrel,
		Bucket,

		Weapon,
		Armor,

		Workbench,

		Corpse,

		/* top */
		Water,
		WaterDouble,

		/* designations */
		DesignationMine,
		DesignationChannel,
	}
}
