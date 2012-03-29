﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Dwarrowdelf
{
	[Serializable]
	[System.ComponentModel.TypeConverter(typeof(IntSize3DConverter))]
	public struct IntSize3 : IEquatable<IntSize3>
	{
		readonly int m_width;
		readonly int m_height;
		readonly int m_depth;

		public int Width { get { return m_width; } }
		public int Height { get { return m_height; } }
		public int Depth { get { return m_depth; } }

		public IntSize3(int width, int height, int depth)
		{
			m_width = width;
			m_height = height;
			m_depth = depth;
		}

		public IntSize3(IntSize2 size, int depth)
		{
			m_width = size.Width;
			m_height = size.Height;
			m_depth = depth;
		}

		public bool IsEmpty
		{
			get { return this.Width == 0 && this.Height == 0 && this.Depth == 0; }
		}

		public bool Contains(IntPoint3 p)
		{
			return p.X >= 0 && p.Y >= 0 && p.Z >= 0 && p.X < this.Width && p.Y < this.Height && p.Z < this.Depth;
		}

		public IntSize2 Plane
		{
			get { return new IntSize2(this.Width, this.Height); }
		}

		public IEnumerable<IntPoint3> Range()
		{
			for (int z = 0; z < this.Depth; ++z)
				for (int y = 0; y < this.Height; ++y)
					for (int x = 0; x < this.Width; ++x)
						yield return new IntPoint3(x, y, z);
		}

		#region IEquatable<IntSize3> Members

		public bool Equals(IntSize3 s)
		{
			return ((s.Width == this.Width) && (s.Height == this.Height) && (s.Depth == this.Depth));
		}

		#endregion

		public override bool Equals(object obj)
		{
			if (!(obj is IntSize3))
				return false;

			IntSize3 s = (IntSize3)obj;
			return ((s.Width == this.Width) && (s.Height == this.Height) && (s.Depth == this.Depth));
		}

		public static bool operator ==(IntSize3 left, IntSize3 right)
		{
			return ((left.Width == right.Width) && (left.Height == right.Height) && (left.Depth == right.Depth));
		}

		public static bool operator !=(IntSize3 left, IntSize3 right)
		{
			return !(left == right);
		}

		public override int GetHashCode()
		{
			return Helpers.Hash3D(this.Width, this.Height, this.Depth);
		}

		public override string ToString()
		{
			var info = System.Globalization.NumberFormatInfo.InvariantInfo;
			return String.Format(info, "{0},{1},{2}", m_width, m_height, m_depth);
		}

		public static IntSize3 Parse(string str)
		{
			var info = System.Globalization.NumberFormatInfo.InvariantInfo;
			var arr = str.Split(',');
			return new IntSize3(Convert.ToInt32(arr[0], info), Convert.ToInt32(arr[1], info), Convert.ToInt32(arr[2], info));
		}
	}
}
