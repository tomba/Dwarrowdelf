﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace MyGame
{
	[DataContract]
	public class MapLocationTerrain
	{
		[DataMember]
		public Location Location { get; set; }
		[DataMember]
		public int Terrain { get; set; }
		[DataMember]
		public ObjectID[] Objects { get; set; }

		public MapLocationTerrain(Location l, int terrain, ObjectID[] objects)
		{
			this.Location = l;
			this.Terrain = terrain;
			this.Objects = objects;
		}
	}

	public enum Direction : byte
	{
		Up = 0,
		Down,
		Left,
		Right,
		UpLeft,
		UpRight,
		DownLeft,
		DownRight
	}

	[DataContract]
	public struct Location : IEquatable<Location>
	{
		[DataMember]
		public int X { get; set; }
		[DataMember]
		public int Y { get; set; }

		public Location(int x, int y)
			: this()
		{
			X = x;
			Y = y;
		}

		#region IEquatable<Location> Members

		public bool Equals(Location l)
		{
			return ((l.X == this.X) && (l.Y == this.Y));
		}

		#endregion

		public override bool Equals(object obj)
		{
			if (!(obj is Location))
				return false;

			Location l = (Location)obj;
			return ((l.X == this.X) && (l.Y == this.Y));
		}

		public static bool operator ==(Location left, Location right)
		{
			return ((left.X == right.X) && (left.Y == right.Y));
		}

		public static bool operator !=(Location left, Location right)
		{
			return !(left == right);
		}

		public static Location operator +(Location left, Location right)
		{
			return new Location(left.X + right.X, left.Y + right.Y);			
		}

		public static Location operator -(Location left, Location right)
		{
			return new Location(left.X - right.X, left.Y - right.Y);
		}

		public override int GetHashCode()
		{
			return (this.X ^ this.Y);
		}

		public override string ToString()
		{
			return String.Format("Location({0}, {1})", X, Y);
		}

	}

}
