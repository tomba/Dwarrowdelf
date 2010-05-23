﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace MyGame
{
	public enum MaterialID : byte
	{
		Undefined,
		Stone,
		Iron,
		Steel,
		Diamond,
		Wood,
		Grass,
	}

	public class MaterialInfo
	{
		public MaterialInfo(MaterialID id)
		{
			this.ID = id;
			this.Name = id.ToString();
		}

		public MaterialID ID { get; private set; }
		public string Name { get; private set; }
	}

	public static class Materials
	{
		static MaterialInfo[] s_materialList;

		static Materials()
		{
			var arr = (MaterialID[])Enum.GetValues(typeof(MaterialID));
			var max = arr.Max();
			s_materialList = new MaterialInfo[(int)max + 1];

			foreach (var field in typeof(Materials).GetFields())
			{
				if (field.FieldType != typeof(MaterialInfo))
					continue;

				var materialInfo = (MaterialInfo)field.GetValue(null);
				s_materialList[(int)materialInfo.ID] = materialInfo;
			}
		}

		public static MaterialInfo GetMaterial(MaterialID id)
		{
			return s_materialList[(int)id];
		}

		public static readonly MaterialInfo Undefined = new MaterialInfo(MaterialID.Undefined);

		public static readonly MaterialInfo Stone = new MaterialInfo(MaterialID.Stone);
		public static readonly MaterialInfo Iron = new MaterialInfo(MaterialID.Iron);
		public static readonly MaterialInfo Steel = new MaterialInfo(MaterialID.Steel);
		public static readonly MaterialInfo Diamond = new MaterialInfo(MaterialID.Diamond);
		public static readonly MaterialInfo Wood = new MaterialInfo(MaterialID.Wood);
		public static readonly MaterialInfo Grass = new MaterialInfo(MaterialID.Grass);
	}
}
