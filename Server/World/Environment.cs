﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Dwarrowdelf.Server
{
	[SaveGameObject(UseRef = true)]
	public class Environment : ServerGameObject, IEnvironment
	{
		internal static Environment Create(World world, EnvironmentBuilder builder)
		{
			var ob = new Environment(builder);
			ob.Initialize(world);
			return ob;
		}

		[SaveGameProperty("Grid", ReaderWriter = typeof(TileGridReaderWriter))]
		TileGrid m_tileGrid;

		// XXX this is quite good for add/remove child, but bad for gettings objects at certain location
		KeyedObjectCollection[] m_contentArray;

		[SaveGameProperty]
		public uint Version { get; private set; }

		[SaveGameProperty]
		public VisibilityMode VisibilityMode { get; private set; }
		[SaveGameProperty]
		public int Width { get; private set; }
		[SaveGameProperty]
		public int Height { get; private set; }
		[SaveGameProperty]
		public int Depth { get; private set; }

		[SaveGameProperty]
		public IntPoint3D HomeLocation { get; set; }

		Dictionary<IntPoint3D, ActionHandlerDelegate> m_actionHandlers = new Dictionary<IntPoint3D, ActionHandlerDelegate>();
		[SaveGameProperty("Buildings", Converter = typeof(BuildingsSetConv))]
		HashSet<BuildingObject> m_buildings;
		HashSet<IntPoint3D> m_waterTiles = new HashSet<IntPoint3D>();

		Environment(SaveGameContext ctx)
			: base(ctx, ObjectType.Environment)
		{
		}

		Environment(EnvironmentBuilder builder)
			: base(ObjectType.Environment)
		{
			this.Version = 1;
			this.VisibilityMode = builder.VisibilityMode;

			m_tileGrid = builder.Grid;
			var size = m_tileGrid.Size;

			this.Width = size.Width;
			this.Height = size.Height;
			this.Depth = size.Depth;

			m_contentArray = new KeyedObjectCollection[this.Depth];
			for (int i = 0; i < size.Depth; ++i)
				m_contentArray[i] = new KeyedObjectCollection();

			m_buildings = new HashSet<BuildingObject>();
		}

		[OnSaveGamePostDeserialization]
		void OnDeserialized()
		{
			m_contentArray = new KeyedObjectCollection[this.Depth];
			for (int i = 0; i < this.Depth; ++i)
				m_contentArray[i] = new KeyedObjectCollection();

			foreach (var ob in this.Inventory)
				m_contentArray[ob.Z].Add(ob);

			ScanWaterTiles();
		}

		protected override void Initialize(World world)
		{
			InitializeTileHiddenStatuses();

			base.Initialize(world);

			world.TickStarting += Tick;
		}

		void InitializeTileHiddenStatuses()
		{
			foreach (var p in this.Bounds.Range())
			{
				if (!GetTerrain(p).IsBlocker)
				{
					m_tileGrid.SetHidden(p, false);
				}
				else
				{
					bool hidden = DirectionExtensions.PlanarDirections
						.Select(d => p + d)
						.Where(pp => this.Contains(pp))
						.All(pp => !GetTerrain(pp).IsSeeThrough);

					if (hidden && this.Contains(p + Direction.Up))
						hidden = !GetTerrain(p + Direction.Up).IsSeeThroughDown;

					m_tileGrid.SetHidden(p, hidden);
				}
			}
		}

		public override void Destruct()
		{
			this.World.TickStarting -= Tick;

			base.Destruct();
		}

		void MapChanged(IntPoint3D l, TileData tileData)
		{
			this.World.AddChange(new MapChange(this, l, tileData));
		}

		public IntRect Bounds2D
		{
			get { return new IntRect(0, 0, this.Width, this.Height); }
		}

		public IntCuboid Bounds
		{
			get { return new IntCuboid(0, 0, 0, this.Width, this.Height, this.Depth); }
		}

		public bool Contains(IntPoint3D p)
		{
			return p.X >= 0 && p.Y >= 0 && p.Z >= 0 && p.X < this.Width && p.Y < this.Height && p.Z < this.Depth;
		}

		public delegate bool ActionHandlerDelegate(ServerGameObject ob, GameAction action);

		public void SetActionHandler(IntPoint3D p, ActionHandlerDelegate handler)
		{
			m_actionHandlers[p] = handler;
		}

		public override bool HandleChildAction(ServerGameObject child, GameAction action)
		{
			ActionHandlerDelegate handler;
			if (m_actionHandlers.TryGetValue(child.Location, out handler) == false)
				return false;

			return handler(child, action);
		}


		public void ScanWaterTiles()
		{
			foreach (var p in this.Bounds.Range())
			{
				if (m_tileGrid.GetWaterLevel(p) > 0)
					m_waterTiles.Add(p);
				else
					m_waterTiles.Remove(p);
			}
		}

		// XXX bad shuffle
		static Random s_waterRandom = new Random();
		static void ShuffleArray(Direction[] array)
		{
			if (array.Length == 0)
				return;

			for (int i = array.Length - 1; i >= 0; i--)
			{
				var tmp = array[i];
				int randomIndex = s_waterRandom.Next(i + 1);

				//Swap elements
				array[i] = array[randomIndex];
				array[randomIndex] = tmp;
			}
		}


		bool CanWaterFlow(IntPoint3D from, IntPoint3D to)
		{
			if (!this.Contains(to))
				return false;

			IntVector3D v = to - from;

			Debug.Assert(v.IsNormal);

			var dstTerrain = GetTerrain(to);
			var dstInter = GetInterior(to);

			if (dstTerrain.IsBlocker || dstInter.IsBlocker)
				return false;

			if (v.Z == 0)
				return true;

			Direction dir = v.ToDirection();

			if (dir == Direction.Up)
				return dstTerrain.IsPermeable == true;

			var srcTerrain = GetTerrain(from);

			if (dir == Direction.Down)
				return srcTerrain.IsPermeable == true;

			throw new Exception();
		}

		void HandleWaterAt(IntPoint3D p, Dictionary<IntPoint3D, int> waterChangeMap)
		{
			int curLevel;

			if (!waterChangeMap.TryGetValue(p, out curLevel))
			{
				curLevel = m_tileGrid.GetWaterLevel(p);
				/* water evaporates */
				/*
				if (curLevel == 1 && s_waterRandom.Next(100) == 0)
				{
					waterChangeMap[p] = 0;
					return;
				}
				 */
			}

			var dirs = DirectionExtensions.CardinalUpDownDirections.ToArray();
			ShuffleArray(dirs);
			bool curLevelChanged = false;

			for (int i = 0; i < dirs.Length; ++i)
			{
				var d = dirs[i];
				var pp = p + d;

				if (!CanWaterFlow(p, pp))
					continue;

				int neighLevel;
				if (!waterChangeMap.TryGetValue(pp, out neighLevel))
					neighLevel = m_tileGrid.GetWaterLevel(pp);

				int flow;
				if (d == Direction.Up)
				{
					if (curLevel > TileData.MaxWaterLevel)
					{
						flow = curLevel - (neighLevel + TileData.MaxCompress) - 1;
						flow = MyMath.Clamp(flow, curLevel - TileData.MaxWaterLevel, 0);
					}
					else
						flow = 0;

				}
				else if (d == Direction.Down)
				{
					if (neighLevel < TileData.MaxWaterLevel)
						flow = TileData.MaxWaterLevel - neighLevel;
					else if (curLevel >= TileData.MaxWaterLevel)
						flow = curLevel - neighLevel + TileData.MaxCompress;
					else
						flow = 0;

					flow = MyMath.Clamp(flow, curLevel, 0);
				}
				else
				{
					if (curLevel > TileData.MinWaterLevel && curLevel > neighLevel)
					{
						int diff = curLevel - neighLevel;
						flow = (diff + 5) / 6;
						Debug.Assert(flow < curLevel);
						//flow = Math.Min(flow, curLevel - 1);
						//flow = IntClamp(flow, curLevel > 1 ? curLevel - 1 : 0, neighLevel > 1 ? -neighLevel + 1 : 0);
					}
					else
					{
						flow = 0;
					}
				}

				if (flow == 0)
					continue;

				curLevel -= flow;
				neighLevel += flow;

				waterChangeMap[pp] = neighLevel;
				curLevelChanged = true;
			}

			if (curLevelChanged)
				waterChangeMap[p] = curLevel;
		}

		void HandleWater()
		{
			IntPoint3D[] waterTiles = m_waterTiles.ToArray();
			Dictionary<IntPoint3D, int> waterChangeMap = new Dictionary<IntPoint3D, int>();

			foreach (var p in waterTiles)
			{
				if (m_tileGrid.GetWaterLevel(p) == 0)
					throw new Exception();

				HandleWaterAt(p, waterChangeMap);
			}

			foreach (var kvp in waterChangeMap)
			{
				var p = kvp.Key;
				int level = kvp.Value;

				var td = m_tileGrid.GetTileData(p);
				td.WaterLevel = (byte)level;
				m_tileGrid.SetWaterLevel(p, (byte)level);

				MapChanged(p, td);

				if (level > 0)
					m_waterTiles.Add(p);
				else
					m_waterTiles.Remove(p);
			}
		}

		void Tick()
		{
			HandleWater();
		}

		public TerrainID GetTerrainID(IntPoint3D l)
		{
			return m_tileGrid.GetTerrainID(l);
		}

		public MaterialID GetTerrainMaterialID(IntPoint3D l)
		{
			return m_tileGrid.GetTerrainMaterialID(l);
		}

		public InteriorID GetInteriorID(IntPoint3D l)
		{
			return m_tileGrid.GetInteriorID(l);
		}

		public MaterialID GetInteriorMaterialID(IntPoint3D l)
		{
			return m_tileGrid.GetInteriorMaterialID(l);
		}

		public TerrainInfo GetTerrain(IntPoint3D l)
		{
			return Terrains.GetTerrain(GetTerrainID(l));
		}

		public MaterialInfo GetTerrainMaterial(IntPoint3D l)
		{
			return Materials.GetMaterial(m_tileGrid.GetTerrainMaterialID(l));
		}

		public InteriorInfo GetInterior(IntPoint3D l)
		{
			return Interiors.GetInterior(GetInteriorID(l));
		}

		public MaterialInfo GetInteriorMaterial(IntPoint3D l)
		{
			return Materials.GetMaterial(m_tileGrid.GetInteriorMaterialID(l));
		}

		public TileData GetTileData(IntPoint3D l)
		{
			return m_tileGrid.GetTileData(l);
		}

		public byte GetWaterLevel(IntPoint3D l)
		{
			return m_tileGrid.GetWaterLevel(l);
		}

		public bool GetGrass(IntPoint3D l)
		{
			return m_tileGrid.GetGrass(l);
		}

		public bool GetHidden(IntPoint3D l)
		{
			return m_tileGrid.GetHidden(l);
		}

		public void SetTerrain(IntPoint3D p, TerrainID terrainID, MaterialID materialID)
		{
			Debug.Assert(this.IsInitialized);
			Debug.Assert(this.World.IsWritable);

			this.Version += 1;

			var oldData = GetTileData(p);

			m_tileGrid.SetTerrain(p, terrainID, materialID);

			var data = m_tileGrid.GetTileData(p);

			MapChanged(p, data);

			RevealTiles(p, oldData, data);
		}

		public void SetInterior(IntPoint3D p, InteriorID interiorID, MaterialID materialID)
		{
			Debug.Assert(this.IsInitialized);
			Debug.Assert(this.World.IsWritable);

			this.Version += 1;

			m_tileGrid.SetInterior(p, interiorID, materialID);

			var data = m_tileGrid.GetTileData(p);

			MapChanged(p, data);
		}

		public void SetTileData(IntPoint3D p, TileData data)
		{
			Debug.Assert(this.IsInitialized);
			Debug.Assert(this.World.IsWritable);

			this.Version += 1;

			var oldData = GetTileData(p);

			m_tileGrid.SetTileData(p, data);

			MapChanged(p, data);

			RevealTiles(p, oldData, data);
		}

		void RevealTiles(IntPoint3D p, TileData oldData, TileData newData)
		{
			var oldTerrain = Terrains.GetTerrain(oldData.TerrainID);
			var newTerrain = Terrains.GetTerrain(newData.TerrainID);

			if (oldTerrain.IsSeeThrough != newTerrain.IsSeeThrough)
			{
				var revealed = DirectionExtensions.PlanarDirections
					.Select(dir => p + dir)
					.Where(pp => this.Contains(pp))
					.Where(pp => m_tileGrid.GetHidden(pp));

				foreach (var pp in revealed)
				{
					m_tileGrid.SetHidden(pp, false);
					MapChanged(pp, m_tileGrid.GetTileData(pp));
				}
			}

			if (oldTerrain.IsSeeThroughDown != newTerrain.IsSeeThroughDown)
			{
				var pp = p + Direction.Down;

				if (this.Contains(pp) && m_tileGrid.GetHidden(pp))
				{
					m_tileGrid.SetHidden(pp, false);
					MapChanged(pp, m_tileGrid.GetTileData(pp));
				}
			}
		}

		public void SetWaterLevel(IntPoint3D l, byte waterLevel)
		{
			Debug.Assert(this.IsInitialized);
			Debug.Assert(this.World.IsWritable);

			this.Version += 1;

			m_tileGrid.SetWaterLevel(l, waterLevel);

			var data = m_tileGrid.GetTileData(l);

			MapChanged(l, data);
		}

		public void SetGrass(IntPoint3D l, bool grass)
		{
			Debug.Assert(this.IsInitialized);
			Debug.Assert(this.World.IsWritable);

			this.Version += 1;

			m_tileGrid.SetGrass(l, grass);

			var d = m_tileGrid.GetTileData(l);

			MapChanged(l, d);
		}

		// XXX not a good func. contents can be changed by the caller
		public IEnumerable<ServerGameObject> GetContents(IntPoint3D l)
		{
			var list = m_contentArray[l.Z];
			return list.Where(o => o.Location == l);
		}

		public IEnumerable<ServerGameObject> Objects()
		{
			for (int z = 0; z < this.Depth; ++z)
				foreach (var ob in m_contentArray[z].AsEnumerable())
					yield return ob;
		}

		protected override void OnChildAdded(ServerGameObject child)
		{
			var list = m_contentArray[child.Z];
			Debug.Assert(!list.Contains(child));
			list.Add(child);
		}

		protected override void OnChildRemoved(ServerGameObject child)
		{
			var list = m_contentArray[child.Z];
			Debug.Assert(list.Contains(child));
			list.Remove(child);
		}

		protected override bool OkToAddChild(ServerGameObject ob, IntPoint3D p)
		{
			Debug.Assert(this.World.IsWritable);

			if (!this.Contains(p))
				return false;

			if (!EnvironmentHelpers.CanEnter(this, p))
				return false;

			return true;
		}

		protected override bool OkToMoveChild(ServerGameObject ob, Direction dir, IntPoint3D dstLoc)
		{
			return EnvironmentHelpers.CanMoveFromTo(this, ob.Location, dir);
		}

		protected override void OnChildMoved(ServerGameObject child, IntPoint3D srcLoc, IntPoint3D dstLoc)
		{
			if (srcLoc.Z == dstLoc.Z)
				return;

			var list = m_contentArray[srcLoc.Z];
			Debug.Assert(list.Contains(child));
			list.Remove(child);

			list = m_contentArray[dstLoc.Z];
			Debug.Assert(!list.Contains(child));
			list.Add(child);
		}

		public IEnumerable<Direction> GetDirectionsFrom(IntPoint3D p)
		{
			return EnvironmentHelpers.GetDirectionsFrom(this, p);
		}

		public void AddBuilding(BuildingObject building)
		{
			Debug.Assert(this.World.IsWritable);

			Debug.Assert(m_buildings.Any(b => b.Area.IntersectsWith(building.Area)) == false);
			Debug.Assert(building.IsInitialized == false);

			m_buildings.Add(building);
		}

		public void RemoveBuilding(BuildingObject building)
		{
			Debug.Assert(this.World.IsWritable);
			Debug.Assert(m_buildings.Contains(building));

			m_buildings.Remove(building);
		}

		public BuildingObject GetBuildingAt(IntPoint3D p)
		{
			return m_buildings.SingleOrDefault(b => b.Contains(p));
		}

		public override BaseGameObjectData Serialize()
		{
			// never called
			throw new Exception();
		}

		public override void SerializeTo(Action<Messages.ClientMessage> writer)
		{
			writer(new Messages.MapDataMessage()
			{
				Environment = this.ObjectID,
				VisibilityMode = this.VisibilityMode,
				Bounds = this.Bounds,
				HomeLocation = this.HomeLocation,
			});

			var bounds = this.Bounds;

			if (bounds.Volume < 2000)
			{
				// Send everything in one message
				var arr = new TileData[bounds.Volume];
				foreach (var p in this.Bounds.Range())
				{
					int idx = bounds.GetIndex(p);
					if (m_tileGrid.GetHidden(p))
						arr[idx] = new TileData() { IsHidden = true };
					else
						arr[idx] = m_tileGrid.GetTileData(p);
				}

				writer(new Messages.MapDataTerrainsMessage()
				{
					Environment = this.ObjectID,
					Bounds = bounds,
					TerrainData = arr,
				});
			}
			else if (bounds.Plane.Area < 2000)
			{
				var plane = bounds.Plane;
				// Send every 2D plane in one message
				var arr = new TileData[plane.Area];
				var msg = new Messages.MapDataTerrainsMessage() { Environment = this.ObjectID };

				for (int z = bounds.Z1; z < bounds.Z2; ++z)
				{
					foreach (var p2d in plane.Range())
					{
						int idx = plane.GetIndex(p2d);
						var p = new IntPoint3D(p2d, z);
						if (m_tileGrid.GetHidden(p))
							arr[idx] = new TileData() { IsHidden = true };
						else
							arr[idx] = m_tileGrid.GetTileData(p);
					}

					msg.Bounds = new IntCuboid(bounds.X1, bounds.Y1, z, bounds.Width, bounds.Height, 1);
					msg.TerrainData = arr;

					writer(msg);
				}
			}
			else
			{
				// Send every line in one message
				var arr = new TileData[this.Width];
				var msg = new Messages.MapDataTerrainsMessage() { Environment = this.ObjectID };

				for (int z = bounds.Z1; z < bounds.Z2; ++z)
				{
					for (int y = bounds.Y1; y < bounds.Y2; ++y)
					{
						for (int x = bounds.X1; x < bounds.X2; ++x)
						{
							IntPoint3D p = new IntPoint3D(x, y, z);
							if (m_tileGrid.GetHidden(p))
								arr[x] = new TileData() { IsHidden = true };
							else
								arr[x] = m_tileGrid.GetTileData(p);
						}

						msg.Bounds = new IntCuboid(bounds.X1, y, z, bounds.Width, 1, 1);
						msg.TerrainData = arr;

						writer(msg);
					}
				}
			}

			for (int z = bounds.Z1; z < bounds.Z2; ++z)
			{
				foreach (var o in m_contentArray[z])
				{
					o.SerializeTo(writer);
				}
			}

			foreach (var o in m_buildings)
			{
				o.SerializeTo(writer);
			}
		}

		public override string ToString()
		{
			return String.Format("Environment({0})", this.ObjectID);
		}

		int AStar.IAStarEnvironment.GetTileWeight(IntPoint3D p)
		{
			return 0;
		}

		IEnumerable<Direction> AStar.IAStarEnvironment.GetValidDirs(IntPoint3D p)
		{
			return EnvironmentHelpers.GetDirectionsFrom(this, p);
		}

		bool Dwarrowdelf.AStar.IAStarEnvironment.CanEnter(IntPoint3D p)
		{
			return EnvironmentHelpers.CanEnter(this, p);
		}

		void Dwarrowdelf.AStar.IAStarEnvironment.Callback(IDictionary<IntPoint3D, Dwarrowdelf.AStar.AStarNode> nodes)
		{
		}

		class BuildingsSetConv : Dwarrowdelf.ISaveGameConverter
		{
			public object ConvertToSerializable(object value)
			{
				var set = (HashSet<BuildingObject>)value;
				return set.ToArray();
			}

			public object ConvertFromSerializable(object value)
			{
				var arr = (BuildingObject[])value;
				return new HashSet<BuildingObject>(arr);
			}

			public Type InputType { get { return typeof(HashSet<BuildingObject>); } }

			public Type OutputType { get { return typeof(BuildingObject[]); } }
		}

		class TileGridReaderWriter : ISaveGameReaderWriter
		{
			public void Write(Newtonsoft.Json.JsonWriter writer, object value)
			{
				var grid = (TileGrid)value;

				writer.WriteStartArray();

				int w = grid.Grid.GetLength(2);
				int h = grid.Grid.GetLength(1);
				int d = grid.Grid.GetLength(0);

				writer.WriteValue(w);
				writer.WriteValue(h);
				writer.WriteValue(d);

				var srcArr = grid.Grid;

				for (int z = 0; z < d; ++z)
					for (int y = 0; y < h; ++y)
						for (int x = 0; x < w; ++x)
							writer.WriteValue(srcArr[z, y, x].ToUInt64());

				writer.WriteEndArray();
			}

			public object Read(Newtonsoft.Json.JsonReader reader)
			{
				Debug.Assert(reader.TokenType == Newtonsoft.Json.JsonToken.StartArray);

				reader.Read();
				int w = (int)(long)reader.Value;
				reader.Read();
				int h = (int)(long)reader.Value;
				reader.Read();
				int d = (int)(long)reader.Value;

				var grid = new TileGrid(new IntSize3D(w, h, d));
				var dstArr = grid.Grid;

				for (int z = 0; z < d; ++z)
					for (int y = 0; y < h; ++y)
						for (int x = 0; x < w; ++x)
						{
							reader.Read();
							var td = TileData.FromUInt64((ulong)(long)reader.Value);
							dstArr[z, y, x] = td;
						}

				reader.Read();
				Debug.Assert(reader.TokenType == Newtonsoft.Json.JsonToken.EndArray);

				return grid;
			}
		}
	}

	class TileGrid
	{
		TileData[, ,] m_grid;
		public TileData[, ,] Grid { get { return m_grid; } }
		public IntSize3D Size { get; private set; }

		TileGrid()
		{
		}

		public TileGrid(IntSize3D size)
		{
			this.Size = size;
			m_grid = new TileData[size.Depth, size.Height, size.Width];
		}

		public TileData GetTileData(IntPoint3D p)
		{
			return m_grid[p.Z, p.Y, p.X];
		}

		public TerrainID GetTerrainID(IntPoint3D p)
		{
			return m_grid[p.Z, p.Y, p.X].TerrainID;
		}

		public MaterialID GetTerrainMaterialID(IntPoint3D p)
		{
			return m_grid[p.Z, p.Y, p.X].TerrainMaterialID;
		}

		public InteriorID GetInteriorID(IntPoint3D p)
		{
			return m_grid[p.Z, p.Y, p.X].InteriorID;
		}

		public MaterialID GetInteriorMaterialID(IntPoint3D p)
		{
			return m_grid[p.Z, p.Y, p.X].InteriorMaterialID;
		}

		public bool GetHidden(IntPoint3D p)
		{
			return m_grid[p.Z, p.Y, p.X].IsHidden;
		}

		public byte GetWaterLevel(IntPoint3D p)
		{
			return m_grid[p.Z, p.Y, p.X].WaterLevel;
		}

		public bool GetGrass(IntPoint3D p)
		{
			return m_grid[p.Z, p.Y, p.X].Grass;
		}


		public void SetTileData(IntPoint3D p, TileData data)
		{
			m_grid[p.Z, p.Y, p.X] = data;
		}

		public void SetTerrain(IntPoint3D p, TerrainID id, MaterialID matID)
		{
			m_grid[p.Z, p.Y, p.X].TerrainID = id;
			m_grid[p.Z, p.Y, p.X].TerrainMaterialID = matID;
		}

		public void SetTerrainID(IntPoint3D p, TerrainID id)
		{
			m_grid[p.Z, p.Y, p.X].TerrainID = id;
		}

		public void SetTerrainMaterialID(IntPoint3D p, MaterialID id)
		{
			m_grid[p.Z, p.Y, p.X].TerrainMaterialID = id;
		}

		public void SetInterior(IntPoint3D p, InteriorID id, MaterialID matID)
		{
			m_grid[p.Z, p.Y, p.X].InteriorID = id;
			m_grid[p.Z, p.Y, p.X].InteriorMaterialID = matID;
		}

		public void SetInteriorID(IntPoint3D p, InteriorID id)
		{
			m_grid[p.Z, p.Y, p.X].InteriorID = id;
		}

		public void SetInteriorMaterialID(IntPoint3D p, MaterialID id)
		{
			m_grid[p.Z, p.Y, p.X].InteriorMaterialID = id;
		}

		public void SetWaterLevel(IntPoint3D p, byte waterLevel)
		{
			m_grid[p.Z, p.Y, p.X].WaterLevel = waterLevel;
		}

		public void SetGrass(IntPoint3D p, bool grass)
		{
			m_grid[p.Z, p.Y, p.X].Grass = grass;
		}

		public void SetHidden(IntPoint3D p, bool hidden)
		{
			m_grid[p.Z, p.Y, p.X].IsHidden = hidden;
		}
	}

	public class EnvironmentBuilder
	{
		TileGrid m_tileGrid;
		IntSize3D m_size;

		public IntCuboid Bounds { get { return new IntCuboid(m_size); } }
		public int Width { get { return m_size.Width; } }
		public int Height { get { return m_size.Height; } }
		public int Depth { get { return m_size.Depth; } }

		public VisibilityMode VisibilityMode { get; set; }

		internal TileGrid Grid { get { return m_tileGrid; } }

		public EnvironmentBuilder(IntSize3D size, VisibilityMode visibilityMode)
		{
			m_size = size;
			m_tileGrid = new TileGrid(size);
			this.VisibilityMode = visibilityMode;
		}

		public Environment Create(World world)
		{
			return Environment.Create(world, this);
		}

		public bool Contains(IntPoint3D p)
		{
			return p.X >= 0 && p.Y >= 0 && p.Z >= 0 && p.X < this.Width && p.Y < this.Height && p.Z < this.Depth;
		}

		public TerrainID GetTerrainID(IntPoint3D l)
		{
			return m_tileGrid.GetTerrainID(l);
		}

		public MaterialID GetTerrainMaterialID(IntPoint3D l)
		{
			return m_tileGrid.GetTerrainMaterialID(l);
		}

		public InteriorID GetInteriorID(IntPoint3D l)
		{
			return m_tileGrid.GetInteriorID(l);
		}

		public MaterialID GetInteriorMaterialID(IntPoint3D l)
		{
			return m_tileGrid.GetInteriorMaterialID(l);
		}

		public TerrainInfo GetTerrain(IntPoint3D l)
		{
			return Terrains.GetTerrain(GetTerrainID(l));
		}

		public MaterialInfo GetTerrainMaterial(IntPoint3D l)
		{
			return Materials.GetMaterial(m_tileGrid.GetTerrainMaterialID(l));
		}

		public InteriorInfo GetInterior(IntPoint3D l)
		{
			return Interiors.GetInterior(GetInteriorID(l));
		}

		public MaterialInfo GetInteriorMaterial(IntPoint3D l)
		{
			return Materials.GetMaterial(m_tileGrid.GetInteriorMaterialID(l));
		}

		public TileData GetTileData(IntPoint3D l)
		{
			return m_tileGrid.GetTileData(l);
		}

		public byte GetWaterLevel(IntPoint3D l)
		{
			return m_tileGrid.GetWaterLevel(l);
		}

		public bool GetGrass(IntPoint3D l)
		{
			return m_tileGrid.GetGrass(l);
		}

		public bool GetHidden(IntPoint3D l)
		{
			return m_tileGrid.GetHidden(l);
		}

		public void SetTerrain(IntPoint3D p, TerrainID terrainID, MaterialID materialID)
		{
			m_tileGrid.SetTerrain(p, terrainID, materialID);
		}

		public void SetInterior(IntPoint3D p, InteriorID interiorID, MaterialID materialID)
		{
			m_tileGrid.SetInterior(p, interiorID, materialID);
		}

		public void SetTileData(IntPoint3D p, TileData data)
		{
			m_tileGrid.SetTileData(p, data);
		}

		public void SetGrass(IntPoint3D l, bool grass)
		{
			m_tileGrid.SetGrass(l, grass);
		}
	}
}
