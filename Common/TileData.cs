﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;

namespace Dwarrowdelf
{
	[Flags]
	public enum TileFlags : ushort
	{
		None = 0,
		ItemBlocks = 1 << 0,	// an item in the tile blocks movement
	}

	[Serializable]
	[StructLayout(LayoutKind.Explicit)]
	public struct TileData
	{
		[FieldOffset(0)]
		public ulong Raw;

		[NonSerialized]
		[FieldOffset(0)]
		public TerrainID TerrainID;
		[NonSerialized]
		[FieldOffset(1)]
		public MaterialID TerrainMaterialID;

		[NonSerialized]
		[FieldOffset(2)]
		public InteriorID InteriorID;
		[NonSerialized]
		[FieldOffset(3)]
		public MaterialID InteriorMaterialID;

		[NonSerialized]
		[FieldOffset(4)]
		public TileFlags Flags;

		[NonSerialized]
		[FieldOffset(6)]
		public byte WaterLevel;

		public bool HasTree { get { return this.InteriorID == InteriorID.Tree || this.InteriorID == InteriorID.Sapling; } }

		public bool IsEmpty { get { return this.Raw == EmptyTileData.Raw; } }

		public const int MinWaterLevel = 1;
		public const int MaxWaterLevel = 7;
		public const int MaxCompress = 1;

		public const int SizeOf = 8;

		public static readonly TileData EmptyTileData = new TileData()
		{
			TerrainID = TerrainID.Empty,
			TerrainMaterialID = MaterialID.Undefined,
			InteriorID = InteriorID.Empty,
			InteriorMaterialID = MaterialID.Undefined,
			WaterLevel = 0,
		};

		public bool IsTerrainFloor
		{
			get
			{
				return this.TerrainID == Dwarrowdelf.TerrainID.NaturalFloor || this.TerrainID == Dwarrowdelf.TerrainID.BuiltFloor;
			}
		}

		/// <summary>
		/// Is Interior empty or a "soft" item that can be removed automatically
		/// </summary>
		public bool IsInteriorClear
		{
			get
			{
				return this.InteriorID == InteriorID.Empty || this.InteriorID == InteriorID.Grass ||
					this.InteriorID == Dwarrowdelf.InteriorID.Sapling;
			}
		}

		public bool IsClear
		{
			get
			{
				return this.IsTerrainFloor && this.IsInteriorClear;
			}
		}
	}
}
