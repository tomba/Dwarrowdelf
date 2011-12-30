﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dwarrowdelf;
using Dwarrowdelf.Server;
using Environment = Dwarrowdelf.Server.EnvironmentObject;

using Dwarrowdelf.TerrainGen;
using System.Threading.Tasks;

namespace MyArea
{
	public sealed class Area
	{
		const int AREA_SIZE = 7;
		const int NUM_SHEEP = 3;
		const int NUM_ORCS = 3;

		Environment m_map1;

		public void InitializeWorld(World world)
		{
			m_map1 = CreateMap1(world);
		}

		IntPoint3D GetRandomSurfaceLocation(Environment env, int zLevel)
		{
			IntPoint3D p;
			int iter = 0;

			do
			{
				if (iter++ > 10000)
					throw new Exception();

				p = new IntPoint3D(Helpers.MyRandom.Next(env.Width), Helpers.MyRandom.Next(env.Height), zLevel);
			} while (!EnvironmentHelpers.CanEnter(env, p));

			return p;
		}

		IntPoint3D GetRandomSubterraneanLocation(EnvironmentObjectBuilder env)
		{
			IntPoint3D p;
			int iter = 0;

			do
			{
				if (iter++ > 10000)
					throw new Exception();

				p = new IntPoint3D(Helpers.MyRandom.Next(env.Width), Helpers.MyRandom.Next(env.Height), Helpers.MyRandom.Next(env.Depth));
			} while (env.GetTerrainID(p) != TerrainID.NaturalWall);

			return p;
		}

		Environment CreateMap1(World world)
		{
			int sizeExp = AREA_SIZE;
			int size = (int)Math.Pow(2, sizeExp);

			Grid2D<double> grid = new Grid2D<double>(size + 1, size + 1);

			DiamondSquare.Render(grid, 10, 5, 0.75);
			Clamper.Clamp(grid, 10);

			var envBuilder = new EnvironmentObjectBuilder(new IntSize3D(size, size, 20), VisibilityMode.GlobalFOV);

			Random r = new Random(123);

			CreateTerrainFromHeightmap(grid, envBuilder);

			int surfaceLevel = FindSurfaceLevel(envBuilder);

			CreateSlopes(envBuilder);

			CreateTrees(envBuilder);

			int posx = envBuilder.Width / 10;
			int posy = 1;

			for (int x = posx; x < posx + 4; ++x)
			{
				int y = posy;

				IntPoint3D p;

				{
					p = new IntPoint3D(x, y++, surfaceLevel);
					envBuilder.SetTerrain(p, TerrainID.NaturalWall, MaterialID.Granite);
					envBuilder.SetInterior(p, InteriorID.Ore, MaterialID.NativeGold);
				}

				{
					p = new IntPoint3D(x, y++, surfaceLevel);
					envBuilder.SetTerrain(p, TerrainID.NaturalWall, MaterialID.Granite);
					envBuilder.SetInterior(p, InteriorID.Ore, MaterialID.Magnetite);
				}

				{
					p = new IntPoint3D(x, y++, surfaceLevel);
					envBuilder.SetTerrain(p, TerrainID.NaturalWall, MaterialID.Granite);
					envBuilder.SetInterior(p, InteriorID.Ore, MaterialID.Chrysoprase);
				}
			}

			var oreMaterials = Materials.GetMaterials(MaterialCategory.Gem).Concat(Materials.GetMaterials(MaterialCategory.Mineral)).Select(mi => mi.ID).ToArray();
			for (int i = 0; i < 30; ++i)
			{
				var p = GetRandomSubterraneanLocation(envBuilder);
				var idx = Helpers.MyRandom.Next(oreMaterials.Length);
				CreateOreCluster(envBuilder, p, oreMaterials[idx]);
			}

			var env = envBuilder.Create(world);
			for (int i = 0; i < 200; ++i)
			{
				var p = new IntPoint3D(i, i, surfaceLevel);
				if (!EnvironmentHelpers.CanEnter(env, p))
					continue;

				for (i = i + 5; i < 200; ++i)
				{
					p = new IntPoint3D(i, i, surfaceLevel);
					if (!EnvironmentHelpers.CanEnter(env, p))
						continue;

					env.HomeLocation = new IntPoint3D(i, i, surfaceLevel);

					break;
				}

				break;
			}

			if (env.HomeLocation == new IntPoint3D())
				throw new Exception();




			// Add items
			for (int i = 0; i < 6; ++i)
				CreateItem(env, ItemID.Gem, GetRandomMaterial(MaterialCategory.Gem), GetRandomSurfaceLocation(env, surfaceLevel));

			for (int i = 0; i < 6; ++i)
				CreateItem(env, ItemID.Rock, GetRandomMaterial(MaterialCategory.Rock), GetRandomSurfaceLocation(env, surfaceLevel));


			CreateWaterTest(env, surfaceLevel);

			posx = env.Width / 10;
			posy = env.Height / 10;

			{
				var builder = new BuildingObjectBuilder(BuildingID.Smith, new IntRectZ(posx, posy, 3, 3, surfaceLevel));
				foreach (var p in builder.Area.Range())
				{
					env.SetTerrain(p, TerrainID.NaturalFloor, MaterialID.Granite);
					env.SetInterior(p, InteriorID.Empty, MaterialID.Undefined);
					env.SetGrass(p, false);
				}
				builder.Create(world, env);
			}

			posx += 4;

			{
				var builder = new BuildingObjectBuilder(BuildingID.Carpenter, new IntRectZ(posx, posy, 3, 3, surfaceLevel));
				foreach (var p in builder.Area.Range())
				{
					env.SetTerrain(p, TerrainID.NaturalFloor, MaterialID.Granite);
					env.SetInterior(p, InteriorID.Empty, MaterialID.Undefined);
					env.SetGrass(p, false);
				}
				builder.Create(world, env);
			}

			posx += 4;

			{
				var builder = new BuildingObjectBuilder(BuildingID.Mason, new IntRectZ(posx, posy, 3, 3, surfaceLevel));
				foreach (var p in builder.Area.Range())
				{
					env.SetTerrain(p, TerrainID.NaturalFloor, MaterialID.Granite);
					env.SetInterior(p, InteriorID.Empty, MaterialID.Undefined);
					env.SetGrass(p, false);
				}
				builder.Create(world, env);
			}

			posy += 4;

			{
				var builder = new BuildingObjectBuilder(BuildingID.Smelter, new IntRectZ(posx, posy, 3, 3, surfaceLevel));
				foreach (var p in builder.Area.Range())
				{
					env.SetTerrain(p, TerrainID.NaturalFloor, MaterialID.Granite);
					env.SetInterior(p, InteriorID.Empty, MaterialID.Undefined);
					env.SetGrass(p, false);
				}
				builder.Create(world, env);
			}

			{
				var p = new IntPoint3D(posx + 5, posy + 1, surfaceLevel);
				CreateItem(env, ItemID.Ore, MaterialID.Tin, p);
				CreateItem(env, ItemID.Ore, MaterialID.Tin, p);
				CreateItem(env, ItemID.Ore, MaterialID.Lead, p);
				CreateItem(env, ItemID.Ore, MaterialID.Lead, p);
				CreateItem(env, ItemID.Ore, MaterialID.Iron, p);
				CreateItem(env, ItemID.Ore, MaterialID.Iron, p);

				CreateItem(env, ItemID.Log, GetRandomMaterial(MaterialCategory.Wood), p);
				CreateItem(env, ItemID.Log, GetRandomMaterial(MaterialCategory.Wood), p);
				CreateItem(env, ItemID.Log, GetRandomMaterial(MaterialCategory.Wood), p);
			}

			{
				var gen = FoodGenerator.Create(env.World);
				gen.MoveTo(env, new IntPoint3D(env.Width / 10 - 2, env.Height / 10 - 2, surfaceLevel));
			}

			AddMonsters(env, surfaceLevel);

			return env;
		}

		MaterialID GetRandomMaterial(MaterialCategory category)
		{
			var materials = Materials.GetMaterials(category).Select(mi => mi.ID).ToArray();
			return materials[Helpers.MyRandom.Next(materials.Length)];
		}

		void CreateItem(Environment env, ItemID itemID, MaterialID materialID, IntPoint3D p)
		{
			var builder = new ItemObjectBuilder(itemID, materialID);
			var item = builder.Create(env.World);
			item.MoveTo(env, p);
		}

		void AddMonsters(Environment env, int surfaceLevel)
		{
			var world = env.World;

			for (int i = 0; i < NUM_SHEEP; ++i)
			{
				var livingBuilder = new LivingObjectBuilder(LivingID.Sheep)
				{
					Color = this.GetRandomColor(),
				};

				var living = livingBuilder.Create(world);
				living.SetAI(new Dwarrowdelf.AI.HerbivoreAI(living));

				living.MoveTo(env, GetRandomSurfaceLocation(env, surfaceLevel));
			}

			for (int i = 0; i < NUM_ORCS; ++i)
			{
				var livingBuilder = new LivingObjectBuilder(LivingID.Orc)
				{
					Color = this.GetRandomColor(),
				};

				var living = livingBuilder.Create(world);
				living.SetAI(new Dwarrowdelf.AI.HerbivoreAI(living));

				Helpers.AddGem(living);
				Helpers.AddBattleGear(living);

				living.MoveTo(env, GetRandomSurfaceLocation(env, surfaceLevel));
			}
		}

		static int FindSurfaceLevel(EnvironmentObjectBuilder env)
		{
			int surfaceLevel = 0;
			int numSurfaces = 0;

			/* find the z level with most surface */
			for (int z = 0; z < env.Depth; ++z)
			{
				int n = env.Bounds.Plane.Range()
					.Select(p => new IntPoint3D(p, z))
					.Where(p => env.GetTerrain(p).IsSupporting && !env.GetTerrain(p).IsBlocker && !env.GetInterior(p).IsBlocker)
					.Count();

				if (n > numSurfaces)
				{
					surfaceLevel = z;
					numSurfaces = n;
				}
			}

			return surfaceLevel;
		}

		static void CreateTerrainFromHeightmap(Grid2D<double> heightMap, EnvironmentObjectBuilder env)
		{
			var plane = env.Bounds.Plane;

			Parallel.For(0, env.Height, y =>
			{
				for (int x = 0; x < env.Width; ++x)
				{
					double d = heightMap[x, y];

					for (int z = 0; z < env.Depth; ++z)
					{
						var p = new IntPoint3D(x, y, z);

						env.SetInterior(p, InteriorID.Empty, MaterialID.Undefined);

						if (d > p.Z)
						{
							env.SetTerrain(p, TerrainID.NaturalWall, MaterialID.Granite);
						}
						else
						{
							if (env.GetTerrainID(p + Direction.Down) == TerrainID.NaturalWall)
							{
								env.SetTerrain(p, TerrainID.NaturalFloor, MaterialID.Granite);
								env.SetGrass(p, true);
							}
							else
							{
								env.SetTerrain(p, TerrainID.Empty, MaterialID.Undefined);
							}
						}
					}
				}
			});
		}

		void CreateTrees(EnvironmentObjectBuilder env)
		{
			var materials = Materials.GetMaterials(MaterialCategory.Wood).ToArray();

			var locations = env.Bounds.Range()
				.Where(p => env.GetTerrainID(p) == TerrainID.NaturalFloor || env.GetTerrainID(p).IsSlope())
				.Where(p => env.GetInteriorID(p) == InteriorID.Empty)
				.Where(p => Helpers.MyRandom.Next() % 8 == 0);

			foreach (var p in locations)
			{
				var material = materials[Helpers.MyRandom.Next(materials.Length)].ID;
				env.SetInterior(p, Helpers.MyRandom.Next() % 2 == 0 ? InteriorID.Tree : InteriorID.Sapling, material);
			}
		}

		static void CreateSlopes(EnvironmentObjectBuilder env)
		{
			/*
			 * su t
			 * s  td
			 *
			 *    ___
			 *    |
			 * ___|
			 *
			 */

			var bounds = env.Bounds;

			var locs = from s in bounds.Range()
					   let su = s + Direction.Up
					   where bounds.Contains(su)
					   where env.GetTerrainID(s) == TerrainID.NaturalFloor && env.GetTerrainID(su) == TerrainID.Empty
					   from d in DirectionExtensions.PlanarDirections
					   let td = s + d
					   let t = s + d + Direction.Up
					   where bounds.Contains(t)
					   where env.GetTerrainID(td) == TerrainID.NaturalWall && env.GetTerrainID(t) == TerrainID.NaturalFloor
					   select new { Location = s, Direction = d };

			foreach (var loc in locs)
			{
				// skip places surrounded by walls
				if (DirectionExtensions.PlanarDirections
					.Where(d => bounds.Contains(loc.Location + d))
					.All(d => env.GetTerrainID(loc.Location + d) != TerrainID.NaturalWall))
					continue;

				env.SetTerrain(loc.Location, loc.Direction.ToSlope(), env.GetTerrainMaterialID(loc.Location));
			}
		}

		void CreateOreCluster(EnvironmentObjectBuilder env, IntPoint3D p, MaterialID oreMaterialID)
		{
			CreateOreCluster(env, p, oreMaterialID, Helpers.MyRandom.Next(6) + 1);
		}

		static void CreateOreCluster(EnvironmentObjectBuilder env, IntPoint3D p, MaterialID oreMaterialID, int count)
		{
			if (!env.Contains(p))
				return;

			if (env.GetTerrainID(p) != TerrainID.NaturalWall)
				return;

			if (env.GetInteriorID(p) == InteriorID.Ore)
				return;

			env.SetInterior(p, InteriorID.Ore, oreMaterialID);

			if (count > 0)
			{
				foreach (var d in DirectionExtensions.CardinalUpDownDirections)
					CreateOreCluster(env, p + d, oreMaterialID, count - 1);
			}
		}

		GameColor GetRandomColor()
		{
			return (GameColor)Helpers.MyRandom.Next(GameColorRGB.NUMCOLORS - 1) + 1;
		}


		static void ClearTile(Environment env, IntPoint3D p)
		{
			env.SetTerrain(p, TerrainID.Empty, MaterialID.Undefined);
			env.SetInterior(p, InteriorID.Empty, MaterialID.Undefined);
		}

		static void ClearInside(Environment env, IntPoint3D p)
		{
			env.SetTerrain(p, TerrainID.NaturalFloor, MaterialID.Granite);
			env.SetInterior(p, InteriorID.Empty, MaterialID.Undefined);
		}

		void CreateWalls(Environment env, IntRectZ area)
		{
			for (int x = area.X1; x < area.X2; ++x)
			{
				for (int y = area.Y1; y < area.Y2; ++y)
				{
					if ((y != area.Y1 && y != area.Y2 - 1) &&
						(x != area.X1 && x != area.X2 - 1))
						continue;

					var p = new IntPoint3D(x, y, area.Z);
					env.SetTerrain(p, TerrainID.NaturalWall, MaterialID.Granite);
					env.SetInterior(p, InteriorID.Empty, MaterialID.Undefined);
				}
			}
		}

		void CreateWater(Environment env, IntRectZ area)
		{
			for (int x = area.X1; x < area.X2; ++x)
			{
				for (int y = area.Y1; y < area.Y2; ++y)
				{
					var p = new IntPoint3D(x, y, area.Z);
					env.SetWaterLevel(p, TileData.MaxWaterLevel);
				}
			}
		}

		void CreateWaterTest(Environment env, int surfaceLevel)
		{
			var pos = new IntPoint3D(10, 30, surfaceLevel);

			CreateWalls(env, new IntRectZ(pos.X, pos.Y, 3, 8, surfaceLevel));
			CreateWater(env, new IntRectZ(pos.X + 1, pos.Y + 1, 1, 6, surfaceLevel));

			int x = 15;
			int y = 30;

			ClearTile(env, new IntPoint3D(x, y, surfaceLevel - 0));
			ClearTile(env, new IntPoint3D(x, y, surfaceLevel - 1));
			ClearTile(env, new IntPoint3D(x, y, surfaceLevel - 2));
			ClearTile(env, new IntPoint3D(x, y, surfaceLevel - 3));
			ClearTile(env, new IntPoint3D(x, y, surfaceLevel - 4));
			ClearInside(env, new IntPoint3D(x + 0, y, surfaceLevel - 5));
			ClearInside(env, new IntPoint3D(x + 1, y, surfaceLevel - 5));
			ClearInside(env, new IntPoint3D(x + 2, y, surfaceLevel - 5));
			ClearInside(env, new IntPoint3D(x + 3, y, surfaceLevel - 5));
			ClearInside(env, new IntPoint3D(x + 4, y, surfaceLevel - 5));
			ClearTile(env, new IntPoint3D(x + 4, y, surfaceLevel - 4));
			ClearTile(env, new IntPoint3D(x + 4, y, surfaceLevel - 3));
			ClearTile(env, new IntPoint3D(x + 4, y, surfaceLevel - 2));
			ClearTile(env, new IntPoint3D(x + 4, y, surfaceLevel - 1));
			ClearTile(env, new IntPoint3D(x + 4, y, surfaceLevel - 0));

			env.ScanWaterTiles();

			{
				// Add a water generator
				var item = WaterGenerator.Create(env.World);
				item.MoveTo(env, new IntPoint3D(pos.X + 1, pos.Y + 2, surfaceLevel));
			}
		}
	}
}
