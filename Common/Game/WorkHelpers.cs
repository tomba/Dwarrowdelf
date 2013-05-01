﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dwarrowdelf
{
	public interface ITerrainFilter
	{
		bool Match(TileData td);
	}

	[Serializable]
	public sealed class TerrainFilter : ITerrainFilter
	{
		EnumBitMask32<TerrainID> m_terrainMask;
		EnumBitMask32<InteriorID> m_interiorMask;

		public TerrainFilter(IEnumerable<TerrainID> terrains, IEnumerable<InteriorID> interiors)
		{
			m_terrainMask = new EnumBitMask32<TerrainID>(terrains);
			m_interiorMask = new EnumBitMask32<InteriorID>(interiors);
		}

		public bool Match(TerrainID terrain, InteriorID interior)
		{
			return m_terrainMask.Get(terrain) && m_interiorMask.Get(interior);
		}

		public bool Match(TileData td)
		{
			return Match(td.TerrainID, td.InteriorID);
		}
	}

	public interface IItemFilter
	{
		bool Match(IItemObject item);
	}

	[Serializable]
	public sealed class OrItemFilter : IItemFilter
	{
		IItemFilter[] m_filters;

		public OrItemFilter(IItemFilter filter1, IItemFilter filter2)
		{
			m_filters = new IItemFilter[] { filter1, filter2 };
		}

		public OrItemFilter(IItemFilter filter1, IItemFilter filter2, IItemFilter filter3)
		{
			m_filters = new IItemFilter[] { filter1, filter2, filter3 };
		}

		public OrItemFilter(params object[] args)
		{
			m_filters = args.Cast<IItemFilter>().ToArray();
		}

		public IItemFilter this[int i]
		{
			get
			{
				return m_filters[i];
			}
		}

		public bool Match(IItemObject item)
		{
			return m_filters.Any(f => f.Match(item));
		}

		public override string ToString()
		{
			return String.Format("({0})", String.Join<IItemFilter>(" OR ", m_filters));
		}
	}

	[Serializable]
	public sealed class AndItemFilter : IItemFilter
	{
		IItemFilter[] m_filters;

		public AndItemFilter(IItemFilter filter1, IItemFilter filter2)
		{
			m_filters = new IItemFilter[] { filter1, filter2 };
		}

		public AndItemFilter(IItemFilter filter1, IItemFilter filter2, IItemFilter filter3)
		{
			m_filters = new IItemFilter[] { filter1, filter2, filter3 };
		}

		public AndItemFilter(params object[] args)
		{
			m_filters = args.Cast<IItemFilter>().ToArray();
		}

		public bool Match(IItemObject item)
		{
			return m_filters.All(f => f.Match(item));
		}

		public override string ToString()
		{
			return String.Format("({0})", String.Join<IItemFilter>(" AND ", m_filters));
		}
	}

	[Serializable]
	public sealed class ItemFilter : IItemFilter
	{
		ItemIDMask m_itemIDMask;
		ItemCategoryMask m_itemCategoryMask;
		MaterialIDMask m_materialIDMask;
		MaterialCategoryMask m_materialCategoryMask;

		public ItemFilter(IEnumerable<ItemID> itemIDs, IEnumerable<ItemCategory> itemCategories,
			IEnumerable<MaterialID> materialIDs, IEnumerable<MaterialCategory> materialCategories)
		{
			m_itemIDMask = new ItemIDMask(itemIDs);
			m_itemCategoryMask = new ItemCategoryMask(itemCategories);
			m_materialIDMask = new MaterialIDMask(materialIDs);
			m_materialCategoryMask = new MaterialCategoryMask(materialCategories);
		}

		public ItemFilter(IEnumerable<ItemID> itemIDs, IEnumerable<MaterialID> materialIDs)
		{
			m_itemIDMask = new ItemIDMask(itemIDs);
			m_itemCategoryMask = new ItemCategoryMask();
			m_materialIDMask = new MaterialIDMask(materialIDs);
			m_materialCategoryMask = new MaterialCategoryMask();
		}

		public ItemFilter(ItemID itemID, MaterialCategory materialCategory)
		{
			m_itemIDMask = new ItemIDMask(itemID);
			m_itemCategoryMask = new ItemCategoryMask();
			m_materialIDMask = new MaterialIDMask();
			m_materialCategoryMask = new MaterialCategoryMask(materialCategory);
		}

		public bool Match(ItemID itemID, ItemCategory itemCategory, MaterialID materialID, MaterialCategory materialCategory)
		{
			return m_itemIDMask.Get(itemID) &&
				m_itemCategoryMask.Get(itemCategory) &&
				m_materialIDMask.Get(materialID) &&
				m_materialCategoryMask.Get(materialCategory);
		}

		public bool Match(IItemObject item)
		{
			return Match(item.ItemID, item.ItemCategory, item.MaterialID, item.MaterialCategory);
		}

		public IEnumerable<ItemID> ItemIDs { get { return m_itemIDMask.EnumValues; } }
		public IEnumerable<ItemCategory> ItemCategories { get { return m_itemCategoryMask.EnumValues; } }
		public IEnumerable<MaterialID> MaterialIDs { get { return m_materialIDMask.EnumValues; } }
		public IEnumerable<MaterialCategory> MaterialCategories { get { return m_materialCategoryMask.EnumValues; } }

		public override string ToString()
		{
			return String.Format("({0}, {1}, {2}, {3})",
				m_itemIDMask, m_itemCategoryMask,
				m_materialIDMask, m_materialCategoryMask);
		}
	}

	public static class WorkHelpers
	{
		public static readonly TerrainFilter ConstructFloorTerrainFilter = new TerrainFilter(
			new TerrainID[] { TerrainID.Empty },
			new InteriorID[] { InteriorID.Empty });

		public static readonly ItemFilter ConstructFloorItemFilter = new ItemFilter(ItemID.Block, MaterialCategory.Rock);


		public static readonly TerrainFilter ConstructPavementTerrainFilter = new TerrainFilter(
			new TerrainID[] { TerrainID.NaturalFloor, TerrainID.BuiltFloor },
			new InteriorID[] { InteriorID.Empty, InteriorID.Grass });

		public static readonly OrItemFilter ConstructPavementItemFilter =
			new OrItemFilter(
				new ItemFilter(ItemID.Block, MaterialCategory.Rock),
				new ItemFilter(ItemID.Log, MaterialCategory.Wood)
			);


		public static readonly TerrainFilter ConstructWallTerrainFilter = new TerrainFilter(
			new TerrainID[] { TerrainID.NaturalFloor, TerrainID.BuiltFloor },
			new InteriorID[] { InteriorID.Empty, InteriorID.Grass });

		public static readonly ItemFilter ConstructWallItemFilter = new ItemFilter(ItemID.Block, MaterialCategory.Rock);
	}
}
