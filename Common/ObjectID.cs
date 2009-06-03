﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace MyGame
{
	[DataContract]
	public struct ObjectID : IEquatable<ObjectID>
	{
		[DataMember]
		readonly int m_value;

		public static readonly ObjectID NullObjectID = new ObjectID(0);

		public ObjectID(int value)
			: this()
		{
			m_value = value;
		}


		#region IEquatable<Location> Members

		public bool Equals(ObjectID objectID)
		{
			return objectID.m_value == m_value;
		}

		#endregion

		public override bool Equals(object obj)
		{
			if (!(obj is ObjectID))
				return false;

			ObjectID objectID = (ObjectID)obj;
			return objectID.m_value == m_value;
		}

		public static bool operator ==(ObjectID left, ObjectID right)
		{
			return left.m_value == right.m_value;
		}

		public static bool operator !=(ObjectID left, ObjectID right)
		{
			return !(left == right);
		}

		public override int GetHashCode()
		{
			return m_value;
		}

		public override string ToString()
		{
			return String.Format("ObjectID({0})", m_value);
		}

	}

}
