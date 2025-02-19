using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Dwarrowdelf
{
	public enum LivingID
	{
		Undefined = 0,
		Dwarf,
		Sheep,
		Wolf,
		Dragon,
		Orc,
	}

	[Flags]
	public enum LivingCategory
	{
		None = 0,
		Civilized = 1 << 0,
		Herbivore = 1 << 1,
		Carnivore = 1 << 2,
		Monster = 1 << 3,
	}

	public enum LivingGender
	{
		Undefined,
		Male,
		Female,
	}

	public sealed class LivingInfo
	{
		public LivingID ID { get; set; }
		public string Name { get; set; }
		public LivingCategory Category { get; set; }
		public GameColor Color { get; set; }
		public int Level { get; set; }
		public int Size { get; set; }
		public int NaturalAC { get; set; }
	}

	public static class Livings
	{
		static LivingInfo[] s_livings;

		static Livings()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var resourceName = "Dwarrowdelf.Game.Livings.json";

			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			using (StreamReader reader = new StreamReader(stream))
			{
				var json = reader.ReadToEnd();
				var options = new JsonSerializerOptions { 
					Converters = {
						new JsonStringEnumConverter<LivingID>(),
						new JsonStringEnumConverter<LivingCategory>(),
						new JsonStringEnumConverter<GameColor>()
						}
					};
				var livings = JsonSerializer.Deserialize<LivingInfo[]>(json, options);

				var max = livings.Max(i => (int)i.ID);
				s_livings = new LivingInfo[max + 1];

				foreach (var living in livings)
				{
					if (s_livings[(int)living.ID] != null)
						throw new Exception();

					if (living.Name == null)
						living.Name = living.ID.ToString().ToLowerInvariant();

					s_livings[(int)living.ID] = living;
				}
			}
		}

		public static LivingInfo GetLivingInfo(LivingID livingID)
		{
			Debug.Assert(livingID != LivingID.Undefined);
			Debug.Assert(s_livings[(int)livingID] != null);

			return s_livings[(int)livingID];
		}

		public static IEnumerable<LivingInfo> GetLivingInfos(LivingCategory category)
		{
			Debug.Assert(category != LivingCategory.None);

			return s_livings.Skip(1).Where(li => (li.Category & category) != 0);
		}
	}
}
