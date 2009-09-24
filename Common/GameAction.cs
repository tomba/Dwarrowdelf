﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;

namespace MyGame
{
	[DataContract,
	KnownType(typeof(MoveAction)),
	KnownType(typeof(WaitAction)),
	KnownType(typeof(GetAction)),
	KnownType(typeof(DropAction)),
	KnownType(typeof(MineAction)),
	]
	public abstract class GameAction
	{
		[DataMember]
		public ObjectID ActorObjectID { get; set; }
		[DataMember]
		public int TransactionID { get; set; }

		public int UserID { get; set; }
		public int TurnsLeft { get; set; }

		public GameAction()
		{
		}
	}

	[DataContract]
	public class MoveAction: GameAction
	{
		[DataMember]
		public Direction Direction { get; set; }

		public MoveAction(Direction direction)
		{
			this.Direction = direction;
		}

		public override string ToString()
		{
			return String.Format("MoveAction({0}, left {1})", this.Direction, this.TurnsLeft);
		}
	}

	[DataContract]
	public class WaitAction : GameAction
	{
		[DataMember]
		public int WaitTurns { get; set; }

		public WaitAction(int turns)
		{
			this.WaitTurns = turns;
		}

		public override string ToString()
		{
			return String.Format("WaitAction({0})", this.WaitTurns);
		}
	}

	[DataContract]
	public class DropAction : GameAction
	{
        [DataMember]
        public IEnumerable<ObjectID> ItemObjectIDs { get; set; }

        public DropAction(IEnumerable<GameObject> items)
        {
            this.ItemObjectIDs = items.Select(i => i.ObjectID).ToArray();
        }

		public override string ToString()
		{
			return String.Format("DropAction({0})",
				String.Join(", ", this.ItemObjectIDs.Select(i => i.ToString()).ToArray()));
		}
	}

	[DataContract]
	public class GetAction : GameAction
	{
		[DataMember]
		public IEnumerable<ObjectID> ItemObjectIDs { get; set; }

		public GetAction(IEnumerable<GameObject> items)
        {
            this.ItemObjectIDs = items.Select(i => i.ObjectID).ToArray();
        }

		public override string ToString()
		{
			return String.Format("GetAction({0})",
				String.Join(", ", this.ItemObjectIDs.Select(i => i.ToString()).ToArray()));
		}
	}

	[DataContract]
	public class MineAction : GameAction
	{
		[DataMember]
		public Direction Direction { get; set; }

		public MineAction(Direction dir)
		{
			this.Direction = dir;
		}

		public override string ToString()
		{
			return String.Format("MineAction({0}, turns: {1})", this.Direction, this.TurnsLeft);
		}
	}
}
