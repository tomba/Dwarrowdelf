using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Dwarrowdelf
{
	public sealed class WorkbenchInfo
	{
		public ItemID WorkbenchID { get; set; }
		public List<BuildableItem> BuildableItems { get; set; }

		public WorkbenchInfo()
		{
			this.BuildableItems = new List<BuildableItem>();
		}

		public BuildableItem FindBuildableItem(string buildableItemKey)
		{
			return this.BuildableItems.SingleOrDefault(i => i.Key == buildableItemKey);
		}
	}

	public sealed class BuildableItem
	{
		public string Key { get; set; }
		public string FullKey { get; set; }
		public ItemID ItemID { get; set; }
		public ItemInfo ItemInfo { get { return Items.GetItemInfo(this.ItemID); } }
		public MaterialID? MaterialID { get; set; }
		public SkillID SkillID { get; set; }
		public LaborID LaborID { get; set; }
		public List<FixedMaterialFilter> FixedBuildMaterials { get; set; }

		public BuildableItem()
		{
			FixedBuildMaterials = new List<FixedMaterialFilter>();
		}

		public bool MatchBuildItems(IItemObject[] obs)
		{
			var materials = this.FixedBuildMaterials;

			if (obs.Length != materials.Count)
				return false;

			for (int i = 0; i < materials.Count; ++i)
			{
				if (!materials[i].Match(obs[i]))
					return false;
			}

			return true;
		}
	}

	public sealed class FixedMaterialFilter : IItemFilter
	{
		public ItemID? ItemID { get; set; }
		public ItemCategory? ItemCategory { get; set; }
		public MaterialCategory? MaterialCategory { get; set; }
		public MaterialID? MaterialID { get; set; }

		public bool Match(IItemObject ob)
		{
			if (this.ItemID.HasValue && this.ItemID.Value != ob.ItemID)
				return false;

			if (this.ItemCategory.HasValue && this.ItemCategory.Value != ob.ItemCategory)
				return false;

			if (this.MaterialID.HasValue && this.MaterialID.Value != ob.MaterialID)
				return false;

			if (this.MaterialCategory.HasValue && this.MaterialCategory.Value != ob.MaterialCategory)
				return false;

			return true;
		}
	}

	public static class Workbenches
	{
		static Dictionary<ItemID, WorkbenchInfo> s_workbenchInfos;

		static Workbenches()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var resourceName = "Dwarrowdelf.Game.Workbenches.json";

			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			using (StreamReader reader = new StreamReader(stream))
			{
				var json = reader.ReadToEnd();
				var options = new JsonSerializerOptions
				{
					Converters =
					{
						new JsonStringEnumConverter<ItemID>(),
						new JsonStringEnumConverter<SkillID>(),
						new JsonStringEnumConverter<LaborID>(),
						new JsonStringEnumConverter<MaterialID>(),
						new JsonStringEnumConverter<ItemCategory>(),
						new JsonStringEnumConverter<MaterialCategory>()
					}
				};
				var workbenchInfos = JsonSerializer.Deserialize<WorkbenchInfo[]>(json, options);

				s_workbenchInfos = new Dictionary<ItemID, WorkbenchInfo>(workbenchInfos.Length);

				foreach (var workbench in workbenchInfos)
				{
					if (s_workbenchInfos.ContainsKey(workbench.WorkbenchID))
						throw new Exception();

					foreach (var bi in workbench.BuildableItems)
					{
						if (String.IsNullOrEmpty(bi.Key))
							bi.Key = bi.ItemID.ToString();

						bi.FullKey = String.Format("{0},{1}", workbench.WorkbenchID, bi.Key);
					}

					// verify BuildableItem key uniqueness
					var grouped = workbench.BuildableItems.GroupBy(bi => bi.Key);
					foreach (var g in grouped)
						if (g.Count() != 1)
							throw new Exception();

					s_workbenchInfos[workbench.WorkbenchID] = workbench;
				}
			}
		}

		public static WorkbenchInfo GetWorkbenchInfo(ItemID workbenchID)
		{
			Debug.Assert(workbenchID != ItemID.Undefined);
			Debug.Assert(s_workbenchInfos[workbenchID] != null);

			return s_workbenchInfos[workbenchID];
		}

		public static BuildableItem FindBuildableItem(string buildableItemFullKey)
		{
			return s_workbenchInfos.SelectMany(kvp => kvp.Value.BuildableItems).SingleOrDefault(bi => bi.FullKey == buildableItemFullKey);
		}
	}
}
