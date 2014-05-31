﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dwarrowdelf.Server
{
	[SaveGameObject]
	public sealed class EnvironmentObject : ContainerObject, IEnvironmentObject
	{
		public static EnvironmentObject Create(World world, Dwarrowdelf.TerrainGen.TerrainData terrain, VisibilityMode visMode,
			IntPoint3 startLocation)
		{
			var ob = new EnvironmentObject(terrain, visMode, startLocation);
			ob.Initialize(world);
			return ob;
		}

		[SaveGameProperty("Grid", ReaderWriter = typeof(TileGridReaderWriter))]
		TileData[, ,] m_tileGrid;

		byte[,] m_levelMap;

		// XXX this is quite good for add/remove child, but bad for gettings objects at certain location
		KeyedObjectCollection[] m_contentArray;

		[SaveGameProperty]
		public uint Version { get; private set; }

		[SaveGameProperty]
		public VisibilityMode VisibilityMode { get; private set; }

		[SaveGameProperty]
		public int Width { get { return this.Size.Width; } }
		[SaveGameProperty]
		public int Height { get { return this.Size.Height; } }
		[SaveGameProperty]
		public int Depth { get { return this.Size.Depth; } }

		[SaveGameProperty]
		public IntSize3 Size { get; private set; }

		[SaveGameProperty]
		public IntPoint3 StartLocation { get; private set; }

		public event Action<IntPoint3, TileData, TileData> TerrainOrInteriorChanged;

		EnvWaterHandler m_waterHandler;
		EnvTreeHandler m_treeHandler;
		EnvWildlifeHandler m_wildlifeHandler;

		[SaveGameProperty]
		int m_originalNumTrees;

		/* contains all (x,y) coordinates of the env in random order */
		uint[] m_randomXYArray;

		EnvironmentObject(SaveGameContext ctx)
			: base(ctx, ObjectType.Environment)
		{
		}

		EnvironmentObject(Dwarrowdelf.TerrainGen.TerrainData terrain, VisibilityMode visMode, IntPoint3 startLocation)
			: base(ObjectType.Environment)
		{
			this.Version = 1;
			this.VisibilityMode = visMode;

			terrain.GetData(out m_tileGrid, out m_levelMap);

			this.Size = terrain.Size;

			this.StartLocation = startLocation;

			SetSubterraneanFlags();


			m_contentArray = new KeyedObjectCollection[this.Depth];
			for (int i = 0; i < this.Depth; ++i)
				m_contentArray[i] = new KeyedObjectCollection();

			m_originalNumTrees = ParallelEnumerable.Range(0, this.Size.Depth).Sum(z =>
			{
				int sum = 0;
				for (int y = 0; y < this.Size.Height; ++y)
					for (int x = 0; x < this.Size.Width; ++x)
						if (GetTileData(x, y, z).InteriorID.IsTree())
							sum++;

				return sum;
			});
		}

		[OnSaveGamePostDeserialization]
		void OnDeserialized()
		{
			m_contentArray = new KeyedObjectCollection[this.Depth];
			for (int i = 0; i < this.Depth; ++i)
				m_contentArray[i] = new KeyedObjectCollection();

			foreach (var ob in this.Inventory)
				m_contentArray[ob.Z].Add(ob);

			CreateLevelMap();

			CommonInit();
		}

		void CreateLevelMap()
		{
			var levelMap = new byte[this.Size.Height, this.Size.Width];

			Parallel.ForEach(this.Size.Plane.Range(), p =>
			{
				for (int z = this.Size.Depth - 1; z >= 0; --z)
				{
					if (GetTileData(p.X, p.Y, z).IsEmpty == false)
					{
						levelMap[p.Y, p.X] = (byte)z;
						break;
					}
				}
			});

			m_levelMap = levelMap;
		}

		void SetSubterraneanFlags()
		{
			Parallel.ForEach(this.Size.Plane.Range(), p =>
			{
				int d = GetSurfaceLevel(p);

				for (int z = this.Size.Depth - 1; z >= 0; --z)
				{
					if (z < d)
						m_tileGrid[z, p.Y, p.X].Flags |= TileFlags.Subterranean;
					else
						m_tileGrid[z, p.Y, p.X].Flags &= ~TileFlags.Subterranean;
				}
			});
		}

		protected override void Initialize(World world)
		{
			base.Initialize(world);

			CommonInit();
		}

		void CommonInit()
		{
			m_randomXYArray = new uint[this.Width * this.Height];
			for (int i = 0; i < m_randomXYArray.Length; ++i)
			{
				ushort x = (ushort)(i % this.Width);
				ushort y = (ushort)(i / this.Width);
				m_randomXYArray[i] = ((uint)x << 16) | y;
			}
			MyMath.ShuffleArray(m_randomXYArray, this.World.Random);

			if (this.World.GameMode == GameMode.Fortress)
			{
				m_treeHandler = new EnvTreeHandler(this, m_originalNumTrees);
				m_wildlifeHandler = new EnvWildlifeHandler(this);
				m_wildlifeHandler.Init();
			}

			m_waterHandler = new EnvWaterHandler(this);
		}

		public override void Destruct()
		{
			if (m_treeHandler != null)
				m_treeHandler.Destruct();

			if (m_waterHandler != null)
				m_waterHandler.Destruct();

			base.Destruct();
		}

		void MapChanged(IntPoint3 l, TileData tileData)
		{
			this.World.AddChange(new MapChange(this, l, tileData));
		}

		public bool Contains(IntPoint3 p)
		{
			return p.X >= 0 && p.Y >= 0 && p.Z >= 0 && p.X < this.Width && p.Y < this.Height && p.Z < this.Depth;
		}

		// XXX called by SetTerrain script
		public void ScanWaterTiles()
		{
			m_waterHandler.Rescan();
		}

		public IntPoint3 GetRandomSurfaceLocation(int idx)
		{
			uint raw = m_randomXYArray[idx];
			int x = (int)(raw >> 16);
			int y = (int)(raw & 0xffff);
			return GetSurfaceLocation(x, y);
		}

		public IntPoint3 GetRandomEnterableSurfaceLocation()
		{
			int numXYs = this.Width * this.Height;

			int idx = this.World.Random.Next(numXYs);

			for (int i = 0; i < numXYs; ++i)
			{
				var p = GetRandomSurfaceLocation(idx);

				if (this.CanEnter(p))
					return p;

				idx += 1;

				if (idx == numXYs)
					idx = 0;
			}

			throw new Exception();
		}

		public int GetSurfaceLevel(int x, int y)
		{
			return m_levelMap[y, x];
		}

		public int GetSurfaceLevel(IntPoint2 p)
		{
			return m_levelMap[p.Y, p.X];
		}

		void SetSurfaceLevel(IntPoint2 p, byte level)
		{
			m_levelMap[p.Y, p.X] = level;
		}

		public IntPoint3 GetSurfaceLocation(int x, int y)
		{
			return new IntPoint3(x, y, GetSurfaceLevel(x, y));
		}

		public IntPoint3 GetSurfaceLocation(IntPoint2 p)
		{
			return GetSurfaceLocation(p.X, p.Y);
		}

		public TerrainID GetTerrainID(IntPoint3 p)
		{
			return GetTileData(p).TerrainID;
		}

		public MaterialID GetTerrainMaterialID(IntPoint3 p)
		{
			return GetTileData(p).TerrainMaterialID;
		}

		public InteriorID GetInteriorID(IntPoint3 p)
		{
			return GetTileData(p).InteriorID;
		}

		public MaterialID GetInteriorMaterialID(IntPoint3 p)
		{
			return GetTileData(p).InteriorMaterialID;
		}

		public TerrainInfo GetTerrain(IntPoint3 p)
		{
			return Terrains.GetTerrain(GetTerrainID(p));
		}

		public MaterialInfo GetTerrainMaterial(IntPoint3 p)
		{
			return Materials.GetMaterial(GetTerrainMaterialID(p));
		}

		public InteriorInfo GetInterior(IntPoint3 p)
		{
			return Interiors.GetInterior(GetInteriorID(p));
		}

		public MaterialInfo GetInteriorMaterial(IntPoint3 p)
		{
			return Materials.GetMaterial(GetInteriorMaterialID(p));
		}

		public TileData GetTileData(IntPoint3 p)
		{
			return m_tileGrid[p.Z, p.Y, p.X];
		}

		public TileData GetTileData(int x, int y, int z)
		{
			return m_tileGrid[z, y, x];
		}

		public byte GetWaterLevel(IntPoint3 p)
		{
			return GetTileData(p).WaterLevel;
		}

		public bool GetTileFlags(IntPoint3 p, TileFlags flags)
		{
			return (GetTileData(p).Flags & flags) != 0;
		}

		/// <summary>
		/// Note: this does not change tile flags!
		/// </summary>
		public void SetTileData(IntPoint3 p, TileData data)
		{
			Debug.Assert(this.IsInitialized);
			Debug.Assert(this.World.IsWritable);

			this.Version += 1;

			var oldData = GetTileData(p);

			// retain the old flags
			Debug.Assert(data.Flags == oldData.Flags || data.Flags == 0);
			data.Flags = oldData.Flags;

			m_tileGrid[p.Z, p.Y, p.X] = data;

			var p2d = p.ToIntPoint2();
			int oldSurfaceLevel = GetSurfaceLevel(p2d);
			int newSurfaceLevel = oldSurfaceLevel;

			if (data.IsEmpty == false && oldSurfaceLevel < p.Z)
			{
				// surface level has risen
				Debug.Assert(p.Z >= 0 && p.Z < 256);
				SetSurfaceLevel(p2d, (byte)p.Z);
				newSurfaceLevel = p.Z;
			}
			else if (data.IsEmpty && oldSurfaceLevel == p.Z)
			{
				// surface level has lowered

				if (p.Z == 0)
					throw new Exception();

				for (int z = p.Z - 1; z >= 0; --z)
				{
					if (GetTileData(new IntPoint3(p2d, z)).IsEmpty == false)
					{
						Debug.Assert(z >= 0 && z < 256);
						SetSurfaceLevel(p2d, (byte)p.Z);
						newSurfaceLevel = z;
						break;
					}
				}
			}

			MapChanged(p, data);

			if (this.TerrainOrInteriorChanged != null)
				this.TerrainOrInteriorChanged(p, oldData, data);

			if (data.WaterLevel > 0)
				m_waterHandler.AddWater(p);
			else
				m_waterHandler.RemoveWater(p);

			if (newSurfaceLevel > oldSurfaceLevel)
			{
				for (int z = oldSurfaceLevel; z < newSurfaceLevel; ++z)
					SetTileFlags(new IntPoint3(p2d, z), TileFlags.Subterranean, true);
			}
			else if (newSurfaceLevel < oldSurfaceLevel)
			{
				for (int z = oldSurfaceLevel - 1; z >= newSurfaceLevel; --z)
					SetTileFlags(new IntPoint3(p2d, z), TileFlags.Subterranean, false);
			}
		}

		public void SetWaterLevel(IntPoint3 p, byte waterLevel)
		{
			Debug.Assert(this.IsInitialized);
			Debug.Assert(this.World.IsWritable);

			this.Version += 1;

			m_tileGrid[p.Z, p.Y, p.X].WaterLevel = waterLevel;

			var data = GetTileData(p);

			MapChanged(p, data);

			if (data.WaterLevel > 0)
				m_waterHandler.AddWater(p);
			else
				m_waterHandler.RemoveWater(p);
		}

		void SetTileFlags(IntPoint3 l, TileFlags flags, bool value)
		{
			Debug.Assert(this.IsInitialized);
			Debug.Assert(this.World.IsWritable);

			this.Version += 1;

			if (value)
				m_tileGrid[l.Z, l.Y, l.X].Flags |= flags;
			else
				m_tileGrid[l.Z, l.Y, l.X].Flags &= ~flags;

			var d = GetTileData(l);

			MapChanged(l, d);
		}

		public void ItemBlockChanged(IntPoint3 p)
		{
			bool oldBlocking = GetTileFlags(p, TileFlags.ItemBlocks);
			bool newBlocking = GetContents(p).OfType<ItemObject>().Any(item => item.IsBlocking);

			if (oldBlocking != newBlocking)
				SetTileFlags(p, TileFlags.ItemBlocks, newBlocking);
		}

		public IEnumerable<IMovableObject> GetContents(IntGrid2Z rect)
		{
			var obs = m_contentArray[rect.Z];

			return obs.Where(o => rect.Contains(o.Location));
		}

		IEnumerable<IMovableObject> IEnvironmentObject.GetContents(IntPoint3 l)
		{
			var list = m_contentArray[l.Z];
			return list.Where(o => o.Location == l);
		}

		public IEnumerable<MovableObject> GetContents(IntPoint3 l)
		{
			var list = m_contentArray[l.Z];
			return list.Where(o => o.Location == l);
		}

		public bool HasContents(IntPoint3 l)
		{
			var list = m_contentArray[l.Z];
			return list.Any(o => o.Location == l);
		}

		public override bool OkToAddChild(MovableObject ob, IntPoint3 p)
		{
			Debug.Assert(this.World.IsWritable);

			if (!this.Contains(p))
				return false;

			if (!this.CanEnter(p))
				return false;

			return true;
		}

		protected override void OnChildAdded(MovableObject child)
		{
			var list = m_contentArray[child.Z];
			Debug.Assert(!list.Contains(child));
			list.Add(child);
		}

		protected override void OnChildRemoved(MovableObject child)
		{
			var list = m_contentArray[child.Z];
			Debug.Assert(list.Contains(child));
			list.Remove(child);
		}


		public override bool OkToMoveChild(MovableObject ob, Direction dir, IntPoint3 dstLoc)
		{
			return EnvironmentExtensions.CanMoveFromTo(this, ob.Location, dir);
		}

		protected override void OnChildMoved(MovableObject child, IntPoint3 srcLoc, IntPoint3 dstLoc)
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


		protected override void CollectObjectData(BaseGameObjectData baseData, ObjectVisibility visibility)
		{
			base.CollectObjectData(baseData, visibility);

			var data = (EnvironmentObjectData)baseData;

			data.VisibilityMode = this.VisibilityMode;
			data.Size = this.Size;
		}

		public void SendIntroTo(IPlayer player)
		{
			var data = new EnvironmentObjectData();
			CollectObjectData(data, ObjectVisibility.Public);
			player.Send(new Messages.ObjectDataMessage(data));
		}

		public override void SendTo(IPlayer player, ObjectVisibility visibility)
		{
			Debug.Assert(visibility != ObjectVisibility.None);

			var data = new EnvironmentObjectData();
			CollectObjectData(data, visibility);
			player.Send(new Messages.ObjectDataMessage(data));

			var sw = Stopwatch.StartNew();
			SendMapTiles(player);
			sw.Stop();
			Trace.TraceInformation("Sending MapTiles took {0} ms", sw.ElapsedMilliseconds);

			foreach (var ob in this.Inventory)
			{
				var vis = player.GetObjectVisibility(ob);

				if (vis != ObjectVisibility.None)
					ob.SendTo(player, vis);
			}

			player.Send(new Messages.ObjectDataEndMessage() { ObjectID = this.ObjectID });
		}

		bool m_useCompression = false;
		bool m_useParallelSend = false;

		void SendMapTiles(IPlayer player)
		{
			if (m_useParallelSend)
				SendMapTilesParallel(player);
			else
				SendMapTilesNonParallel(player);
		}

		void SendMapTilesNonParallel(IPlayer player)
		{
			var visionTracker = player.GetVisionTracker(this);

			int w = this.Width;
			int h = this.Height;
			int d = this.Depth;

			var size = new IntSize3(w, h, 1);

			using (var memStream = new MemoryStream(size.Volume * TileData.SizeOf))
			{
				for (int z = 0; z < d; ++z)
				{
					memStream.SetLength(0);

					var bounds = new IntGrid3(new IntPoint3(0, 0, z), size);

					if (m_useCompression == false)
					{
						WriteTileData(memStream, bounds, visionTracker);
					}
					else
					{
						using (var compressStream = new DeflateStream(memStream, CompressionMode.Compress, true))
						using (var bufferedStream = new BufferedStream(compressStream))
							WriteTileData(bufferedStream, bounds, visionTracker);
					}

					var arr = memStream.ToArray();

					var msg = new Messages.MapDataTerrainsMessage()
					{
						Environment = this.ObjectID,
						Bounds = bounds,
						IsTerrainDataCompressed = m_useCompression,
						TerrainData = arr,
					};

					player.Send(msg);
					//Trace.TraceError("Sent {0}", z);
				}
			}
		}

		void SendMapTilesParallel(IPlayer player)
		{
			var visionTracker = player.GetVisionTracker(this);

			int w = this.Width;
			int h = this.Height;
			int d = this.Depth;

			var queue = new BlockingCollection<Tuple<int, byte[]>>();

			var writerTask = Task.Factory.StartNew(() =>
			{
				foreach (var tuple in queue.GetConsumingEnumerable())
				{
					int z = tuple.Item1;
					var arr = tuple.Item2;

					var msg = new Messages.MapDataTerrainsMessage()
					{
						Environment = this.ObjectID,
						Bounds = new IntGrid3(0, 0, z, w, h, 1),
						IsTerrainDataCompressed = m_useCompression,
						TerrainData = arr,
					};

					player.Send(msg);
					//Trace.TraceError("Sent {0}", z);
				}
			});

			Parallel.For(0, d, z =>
			{
				var bounds = new IntGrid3(0, 0, z, w, h, 1);

				using (var memStream = new MemoryStream())
				{
					if (m_useCompression == false)
					{
						WriteTileData(memStream, bounds, visionTracker);
					}
					else
					{
						using (var compStream = new System.IO.Compression.DeflateStream(memStream, CompressionMode.Compress))
						using (var bufferStream = new BufferedStream(compStream))
							WriteTileData(bufferStream, bounds, visionTracker);
					}

					queue.Add(new Tuple<int, byte[]>(z, memStream.ToArray()));
				}
			});

			queue.CompleteAdding();

			writerTask.Wait();
		}

		void WriteTileData(Stream stream, IntGrid3 bounds, IVisionTracker visionTracker)
		{
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
			{
				foreach (var p in bounds.Range())
				{
					ulong v;

					if (!visionTracker.Sees(p))
						v = 0;
					else
						v = GetTileData(p).Raw;

					writer.Write(v);
				}
			}
		}

		public override string ToString()
		{
			return String.Format("Environment({0})", this.ObjectID);
		}
	}
}
