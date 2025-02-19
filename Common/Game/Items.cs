using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Dwarrowdelf
{
	[Flags]
	public enum ItemFlags
	{
		None = 0,
		Installable = 1 << 0,
		Container = 1 << 1,
	}

	public enum ItemID
	{
		Undefined = 0,
		Log,
		Food,
		Drink,
		Rock,
		Ore,
		Gem,
		UncutGem,
		Block,
		Bar,
		Corpse,

		Chair,
		Table,
		Door,
		Bed,
		Barrel,
		Bin,

		CarpentersTools,
		MasonsTools,

		SmithsWorkbench,
		CarpentersWorkbench,
		MasonsWorkbench,
		SmelterWorkbench,
		GemcuttersWorkbench,

		Dagger,
		ShortSword,
		BattleAxe,
		Mace,

		ChainMail,
		PlateMail,

		Skullcap,
		Helmet,

		Gloves,
		Gauntlets,

		Boots,
		Sandals,

		Contraption,
	}

	[Serializable]
	public sealed class ItemIDMask : EnumBitMask<ItemID>
	{
		public ItemIDMask() : base() { }
		public ItemIDMask(ItemID itemID) : base(itemID) { }
		public ItemIDMask(IEnumerable<ItemID> itemIDs) : base(itemIDs) { }
	}

	public enum ItemCategory
	{
		Undefined = 0,

		Furniture,
		Food,
		Drink,
		Gem,
		RawMaterial,
		Corpse,
		Weapon,
		Armor,
		Workbench,
		Utility,
		Tools,
		Other,
	}

	[Serializable]
	[System.ComponentModel.TypeConverter(typeof(ItemCategoryMaskConverter))]
	public sealed class ItemCategoryMask : EnumBitMask32<ItemCategory>
	{
		public ItemCategoryMask() : base() { }
		public ItemCategoryMask(IEnumerable<ItemCategory> categories) : base(categories) { }
	}

	public sealed class ItemInfo
	{
		public ItemID ID { get; set; }
		public string Name { get; set; }
		public ItemCategory Category { get; set; }
		public ItemFlags Flags { get; set; }
		public WeaponInfo WeaponInfo { get; set; }
		public ArmorInfo ArmorInfo { get; set; }
		public int Capacity { get; set; }

		public bool IsInstallable { get { return (this.Flags & ItemFlags.Installable) != 0; } }
		public bool IsContainer { get { return (this.Flags & ItemFlags.Container) != 0; } }
	}

	public enum WeaponType
	{
		Edged,
		Blunt,
	}

	public sealed class WeaponInfo
	{
		public int WC { get; set; }
		public bool IsTwoHanded { get; set; }
		public WeaponType WeaponType { get; set; }
	}

	public enum ArmorSlot
	{
		Undefined = 0,
		Head,
		Hands,
		Torso,
		Feet,
	}

	public sealed class ArmorInfo
	{
		public int AC { get; set; }
		public ArmorSlot Slot { get; set; }
	}

	public static class Items
	{
		static ItemInfo[] s_items;

		static Items()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var resourceName = "Dwarrowdelf.Game.Items.json";

			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			using (StreamReader reader = new StreamReader(stream))
			{
				var json = reader.ReadToEnd();
				var options = new JsonSerializerOptions
				{
					Converters =
					{
						new JsonStringEnumConverter<ItemID>(),
						new JsonStringEnumConverter<ItemCategory>(),
						new JsonStringEnumConverter<ItemFlags>(),
						new JsonStringEnumConverter<WeaponType>(),
						new JsonStringEnumConverter<ArmorSlot>()
					}
				};
				var items = JsonSerializer.Deserialize<ItemInfo[]>(json, options);

				var max = items.Max(i => (int)i.ID);
				s_items = new ItemInfo[max + 1];

				foreach (var item in items)
				{
					if (s_items[(int)item.ID] != null)
						throw new Exception();

					if (item.Name == null)
						item.Name = item.ID.ToString().ToLowerInvariant();

					s_items[(int)item.ID] = item;
				}
			}
		}

		public static ItemInfo GetItemInfo(ItemID id)
		{
			Debug.Assert(id != ItemID.Undefined);
			Debug.Assert(s_items[(int)id] != null);

			return s_items[(int)id];
		}

		public static IEnumerable<ItemInfo> GetItemInfos()
		{
			return s_items.Where(ii => ii != null);
		}

		public static IEnumerable<ItemInfo> GetItemInfos(ItemCategory category)
		{
			Debug.Assert(category != ItemCategory.Undefined);

			return s_items.Where(ii => ii != null && ii.Category == category);
		}

		public static IEnumerable<ItemID> GetItemIDs()
		{
			return EnumHelpers.GetEnumValues<ItemID>().Skip(1);
		}

		public static IEnumerable<ItemCategory> GetItemCategories()
		{
			return EnumHelpers.GetEnumValues<ItemCategory>().Skip(1);
		}
	}
}
