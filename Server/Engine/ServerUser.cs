﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using Dwarrowdelf.Messages;

namespace Dwarrowdelf.Server
{
	[GameObject]
	public abstract class ServerUser
	{
		Dictionary<Type, Action<ClientMessage>> m_handlerMap = new Dictionary<Type, Action<ClientMessage>>();
		World m_world;

		public World World { get { return m_world; } }

		public bool IsCharLoggedIn { get; private set; }

		[GameProperty("UserID")]
		int m_userID;
		public int UserID { get { return m_userID; } }

		// does this user sees all
		[GameProperty("SeeAll")]
		bool m_seeAll = true;
		public bool IsSeeAll { get { return m_seeAll; } }

		[GameProperty("Controllables")]
		List<Living> m_controllables;
		public ReadOnlyCollection<Living> Controllables { get; private set; }

		ServerConnection m_connection;

		IPRunner m_ipRunner;

		ChangeHandler m_changeHandler;

		MyTraceSource trace = new MyTraceSource("Dwarrowdelf.Connection");

		GameEngine m_engine;

		protected ServerUser()
		{
		}

		public ServerUser(int userID)
		{
			m_userID = userID;

			m_controllables = new List<Living>();
			this.Controllables = new ReadOnlyCollection<Living>(m_controllables);
			m_changeHandler = new ChangeHandler(this);
		}

		[OnGameDeserialized]
		void OnDeserialized()
		{
			this.Controllables = new ReadOnlyCollection<Living>(m_controllables);
			m_changeHandler = new ChangeHandler(this);
		}

		public void Init(GameEngine engine)
		{
			m_engine = engine;
			m_world = m_engine.World;

			trace.Header = String.Format("User({0})", m_userID);
			trace.TraceInformation("New User");
		}

		public void SetConnection(ServerConnection connection)
		{
			trace.TraceInformation("SetConnection");

			m_connection = connection;

			m_ipRunner = new IPRunner(m_world, Send);

			m_world.WorkEnded += HandleEndOfWork;
			m_world.WorldChanged += HandleWorldChange;

			if (m_seeAll)
			{
				foreach (var env in m_world.Environments)
					env.SerializeTo(Send);
			}
		}

		public void UnsetConnection()
		{
			trace.TraceInformation("UnsetConnection");

			if (this.IsCharLoggedIn)
				ExitGame();

			m_world.WorkEnded -= HandleEndOfWork;
			m_world.WorldChanged -= HandleWorldChange;

			m_ipRunner = null;
			m_connection = null;
		}

		void EnterGame()
		{
			Debug.Assert(!this.IsCharLoggedIn);

			var controllables = CreateControllables();
			foreach (var l in controllables)
			{
				l.Destructed += OnPlayerDestructed;
				m_controllables.Add(l);
			}

			this.IsCharLoggedIn = true;
			Send(new Messages.EnterGameReplyMessage());
			Send(new Messages.ControllablesDataMessage() { Controllables = m_controllables.Select(l => l.ObjectID).ToArray() });
		}

		protected abstract Living[] CreateControllables();

		void OnPlayerDestructed(BaseGameObject ob)
		{
			var living = (Living)ob;
			m_controllables.Remove(living);
			living.Destructed -= OnPlayerDestructed;
			Send(new Messages.ControllablesDataMessage() { Controllables = m_controllables.Select(l => l.ObjectID).ToArray() });
		}

		void ExitGame()
		{
			Debug.Assert(this.IsCharLoggedIn);

			Send(new Messages.ControllablesDataMessage() { Controllables = new ObjectID[0] });
			Send(new Messages.ExitGameReplyMessage());

			this.IsCharLoggedIn = false;
		}

		public void Send(ServerMessage msg)
		{
			m_connection.Send(msg);
		}

		public void Send(IEnumerable<ServerMessage> msgs)
		{
			foreach (var msg in msgs)
				Send(msg);
		}

		public void OnReceiveMessage(Message m)
		{
			trace.TraceVerbose("OnReceiveMessage({0})", m);

			var msg = (ClientMessage)m;

			Action<ClientMessage> f;
			Type t = msg.GetType();
			if (!m_handlerMap.TryGetValue(t, out f))
			{
				System.Reflection.MethodInfo mi;
				f = WrapperGenerator.CreateHandlerWrapper<ClientMessage>("ReceiveMessage", t, this, out mi);

				if (f == null)
					throw new Exception(String.Format("No msg handler for {0}", msg.GetType()));

				m_handlerMap[t] = f;
			}

			f(msg);
		}

		void ReceiveMessage(SetTilesMessage msg)
		{
			ObjectID mapID = msg.MapID;
			IntCuboid r = msg.Cube;

			var env = m_world.Environments.SingleOrDefault(e => e.ObjectID == mapID);
			if (env == null)
				throw new Exception();

			foreach (var p in r.Range())
			{
				if (!env.Bounds.Contains(p))
					continue;

				var tileData = env.GetTileData(p);

				if (msg.InteriorID.HasValue)
					tileData.InteriorID = msg.InteriorID.Value;
				if (msg.InteriorMaterialID.HasValue)
					tileData.InteriorMaterialID = msg.InteriorMaterialID.Value;

				if (msg.FloorID.HasValue)
					tileData.FloorID = msg.FloorID.Value;
				if (msg.FloorMaterialID.HasValue)
					tileData.FloorMaterialID = msg.FloorMaterialID.Value;

				if (msg.WaterLevel.HasValue)
					tileData.WaterLevel = msg.WaterLevel.Value;
				if (msg.Grass.HasValue)
					tileData.Grass = msg.Grass.Value;

				env.SetTileData(p, tileData);
			}

			env.ScanWaterTiles();
		}

		void ReceiveMessage(SetWorldConfigMessage msg)
		{
			if (msg.MinTickTime.HasValue)
				m_engine.SetMinTickTime(msg.MinTickTime.Value);
		}

		void ReceiveMessage(CreateBuildingMessage msg)
		{
			ObjectID mapID = msg.MapID;
			var r = msg.Area;
			var id = msg.ID;

			var env = m_world.Environments.SingleOrDefault(e => e.ObjectID == mapID);
			if (env == null)
				throw new Exception();

			var building = new BuildingObject(id) { Area = r };
			foreach (var p in building.Area.Range())
			{
				env.SetInterior(p, InteriorID.Empty, MaterialID.Undefined);
			}
			building.Initialize(m_world, env);
		}

		Random m_random = new Random();
		IntPoint3D GetRandomSurfaceLocation(Environment env, int zLevel)
		{
			IntPoint3D p;
			int iter = 0;

			do
			{
				if (iter++ > 10000)
					throw new Exception();

				p = new IntPoint3D(m_random.Next(env.Width), m_random.Next(env.Height), zLevel);
			} while (!env.CanEnter(p));

			return p;
		}

		/* functions for livings */
		void ReceiveMessage(EnterGameRequestMessage msg)
		{
			m_world.BeginInvoke(new Action<EnterGameRequestMessage>(LogOnChar), msg);
		}

		void LogOnChar(EnterGameRequestMessage msg)
		{
			string name = msg.Name;

			trace.TraceInformation("LogOnChar {0}", name);

			EnterGame();
		}

		void ReceiveMessage(ExitGameRequestMessage msg)
		{
			trace.TraceInformation("LogOffChar");

			ExitGame();
		}

		public bool StartTurnSent { get; private set; }
		public bool ProceedTurnReceived { get; private set; }

		void ReceiveMessage(ProceedTurnMessage msg)
		{
			try
			{
				if (this.StartTurnSent == false)
					throw new Exception();

				if (this.ProceedTurnReceived == true)
					throw new Exception();

				foreach (var tuple in msg.Actions)
				{
					var actorOid = tuple.Item1;
					var action = tuple.Item2;

					var living = m_controllables.SingleOrDefault(l => l.ObjectID == actorOid);

					if (living == null)
						continue;

					if (action == null)
					{
						if (living.CurrentAction.Priority == ActionPriority.High)
							throw new Exception();

						living.CancelAction();

						continue;
					}

					if (action.Priority > ActionPriority.Normal)
						throw new Exception();

					if (living.HasAction)
					{
						if (living.CurrentAction.Priority <= action.Priority)
							living.CancelAction();
						else
							throw new Exception("already has an action");
					}

					living.DoAction(action, m_userID);
				}

				this.ProceedTurnReceived = true;

				m_engine.SignalWorld();
			}
			catch (Exception e)
			{
				trace.TraceError("Uncaught exception");
				trace.TraceError(e.ToString());
			}
		}

		void ReceiveMessage(IPCommandMessage msg)
		{
			trace.TraceInformation("IronPythonCommand");

			m_ipRunner.Exec(msg.Text);
		}


		void HandleWorldChange(Change change)
		{
			if (change is TickStartChange)
			{
				this.StartTurnSent = false;
				this.ProceedTurnReceived = false;
			}
			else if (change is TurnStartChange)
			{
				var c = (TurnStartChange)change;
				if (c.Living != null && m_controllables.Contains(c.Living))
					return;

				this.StartTurnSent = true;
			}
			else if (change is TurnEndChange)
			{
				var c = (TurnEndChange)change;
				if (c.Living != null && m_controllables.Contains(c.Living))
					return;
			}
			else
			{
				m_changeHandler.HandleWorldChange(change);
				return;
			}

			Send(new ChangeMessage { Change = change });
		}


		void HandleEndOfWork()
		{
			m_changeHandler.HandleEndOfWork();
		}
	}



	class ChangeHandler
	{
		// These are used to determine new tiles and objects in sight
		HashSet<Environment> m_knownEnvironments = new HashSet<Environment>();

		Dictionary<Environment, HashSet<IntPoint3D>> m_oldKnownLocations = new Dictionary<Environment, HashSet<IntPoint3D>>();
		HashSet<ServerGameObject> m_oldKnownObjects = new HashSet<ServerGameObject>();

		Dictionary<Environment, HashSet<IntPoint3D>> m_newKnownLocations = new Dictionary<Environment, HashSet<IntPoint3D>>();
		HashSet<ServerGameObject> m_newKnownObjects = new HashSet<ServerGameObject>();

		ServerUser m_user;

		public ChangeHandler(ServerUser user)
		{
			m_user = user;
		}

		void Send(ServerMessage msg)
		{
			m_user.Send(msg);
		}

		void Send(IEnumerable<ServerMessage> msgs)
		{
			m_user.Send(msgs);
		}

		// Called from the world at the end of work
		public void HandleEndOfWork()
		{
			// if the user sees all, no need to send new terrains/objects
			if (!m_user.IsSeeAll)
				HandleNewTerrainsAndObjects(m_user.Controllables);
		}

		void HandleNewTerrainsAndObjects(IList<Living> friendlies)
		{
			m_oldKnownLocations = m_newKnownLocations;
			m_newKnownLocations = CollectLocations(friendlies);

			m_oldKnownObjects = m_newKnownObjects;
			m_newKnownObjects = CollectObjects(m_newKnownLocations);

			var revealedLocations = CollectRevealedLocations(m_oldKnownLocations, m_newKnownLocations);
			var revealedObjects = CollectRevealedObjects(m_oldKnownObjects, m_newKnownObjects);
			var revealedEnvironments = m_newKnownLocations.Keys.Except(m_knownEnvironments).ToArray();

			m_knownEnvironments.UnionWith(revealedEnvironments);

			SendNewEnvironments(revealedEnvironments);
			SendNewTerrains(revealedLocations);
			SendNewObjects(revealedObjects);
		}

		// Collect all environments and locations that friendlies see
		Dictionary<Environment, HashSet<IntPoint3D>> CollectLocations(IEnumerable<Living> friendlies)
		{
			var knownLocs = new Dictionary<Environment, HashSet<IntPoint3D>>();

			foreach (var l in friendlies)
			{
				if (l.Environment == null)
					continue;

				IEnumerable<IntPoint3D> locList;

				/* for AllVisible maps we don't track visible locations, but we still
				 * need to handle newly visible maps */
				if (l.Environment.VisibilityMode == VisibilityMode.AllVisible || l.Environment.VisibilityMode == VisibilityMode.GlobalFOV)
					locList = new List<IntPoint3D>();
				else
					locList = l.GetVisibleLocations().Select(p => new IntPoint3D(p.X, p.Y, l.Z));

				if (!knownLocs.ContainsKey(l.Environment))
					knownLocs[l.Environment] = new HashSet<IntPoint3D>();

				knownLocs[l.Environment].UnionWith(locList);
			}

			return knownLocs;
		}

		// Collect all objects in the given location map
		static HashSet<ServerGameObject> CollectObjects(Dictionary<Environment, HashSet<IntPoint3D>> knownLocs)
		{
			var knownObs = new HashSet<ServerGameObject>();

			foreach (var kvp in knownLocs)
			{
				var env = kvp.Key;
				var newLocs = kvp.Value;

				foreach (var p in newLocs)
				{
					var obList = env.GetContents(p);
					if (obList != null)
						knownObs.UnionWith(obList);
				}
			}

			return knownObs;
		}

		// Collect locations that are newly visible
		static Dictionary<Environment, HashSet<IntPoint3D>> CollectRevealedLocations(Dictionary<Environment, HashSet<IntPoint3D>> oldLocs,
			Dictionary<Environment, HashSet<IntPoint3D>> newLocs)
		{
			var revealedLocs = new Dictionary<Environment, HashSet<IntPoint3D>>();

			foreach (var kvp in newLocs)
			{
				if (oldLocs.ContainsKey(kvp.Key))
					revealedLocs[kvp.Key] = new HashSet<IntPoint3D>(kvp.Value.Except(oldLocs[kvp.Key]));
				else
					revealedLocs[kvp.Key] = kvp.Value;
			}

			return revealedLocs;
		}

		// Collect objects that are newly visible
		static ServerGameObject[] CollectRevealedObjects(HashSet<ServerGameObject> oldObjects, HashSet<ServerGameObject> newObjects)
		{
			var revealedObs = newObjects.Except(oldObjects).ToArray();
			return revealedObs;
		}

		private void SendNewEnvironments(IEnumerable<Environment> revealedEnvironments)
		{
			// send full data for AllVisible envs, and intro for other maps
			foreach (var env in revealedEnvironments)
			{
				if (env.VisibilityMode == VisibilityMode.AllVisible || env.VisibilityMode == VisibilityMode.GlobalFOV)
				{
					env.SerializeTo(Send);
				}
				else
				{
					var msg = new Messages.MapDataMessage()
					{
						Environment = env.ObjectID,
						VisibilityMode = env.VisibilityMode,
					};
					Send(msg);
				}
			}
		}

		void SendNewTerrains(Dictionary<Environment, HashSet<IntPoint3D>> revealedLocations)
		{
			var msgs = revealedLocations.Where(kvp => kvp.Value.Count() > 0).
				Select(kvp => (Messages.ServerMessage)new Messages.MapDataTerrainsListMessage()
				{
					Environment = kvp.Key.ObjectID,
					TileDataList = kvp.Value.Select(l =>
						new Tuple<IntPoint3D, TileData>(l, kvp.Key.GetTileData(l))
						).ToArray(),
				});


			Send(msgs);
		}

		void SendNewObjects(IEnumerable<ServerGameObject> revealedObjects)
		{
			var msgs = revealedObjects.Select(o => new ObjectDataMessage() { ObjectData = o.Serialize() });
			Send(msgs);
		}


		public void HandleWorldChange(Change change)
		{
			// can any friendly see the change?
			if (!m_user.IsSeeAll && !CanSeeChange(change, m_user.Controllables))
				return;

			if (!m_user.IsSeeAll)
			{
				// We don't collect newly visible terrains/objects on AllVisible maps.
				// However, we still need to tell about newly created objects that come
				// to AllVisible maps.
				var c = change as ObjectMoveChange;
				if (c != null && c.Source != c.Destination && c.Destination is Environment &&
					(((Environment)c.Destination).VisibilityMode == VisibilityMode.AllVisible || ((Environment)c.Destination).VisibilityMode == VisibilityMode.GlobalFOV))
				{
					var newObject = (ServerGameObject)c.Object;
					var newObMsg = ObjectToMessage(newObject);
					Send(newObMsg);
				}
			}

			var changeMsg = new ChangeMessage { Change = change };

			Send(changeMsg);

			// XXX this is getting confusing...
			if (m_user.IsSeeAll && change is ObjectCreatedChange)
			{
				var c = (ObjectCreatedChange)change;
				var newObject = (BaseGameObject)c.Object;
				var newObMsg = ObjectToMessage(newObject);
				Send(newObMsg);
			}
		}

		bool CanSeeChange(Change change, IList<Living> controllables)
		{
			// XXX these checks are not totally correct. objects may have changed after
			// the creation of the change, for example moved. Should changes contain
			// all the information needed for these checks?
			if (change is ObjectMoveChange)
			{
				var c = (ObjectMoveChange)change;

				return controllables.Any(l =>
				{
					if (l == c.Object)
						return true;

					if (l.Sees(c.Source, c.SourceLocation))
						return true;

					if (l.Sees(c.Destination, c.DestinationLocation))
						return true;

					return false;
				});
			}
			else if (change is MapChange)
			{
				var c = (MapChange)change;
				return controllables.Any(l => l.Sees(c.Environment, c.Location));
			}
			else if (change is TurnStartChange)
			{
				var c = (TurnStartChange)change;
				return c.Living == null || controllables.Contains(c.Living);
			}
			else if (change is TurnEndChange)
			{
				var c = (TurnEndChange)change;
				return c.Living == null || controllables.Contains(c.Living);
			}

			else if (change is FullObjectChange)
			{
				var c = (FullObjectChange)change;
				return controllables.Contains(c.Object);
			}
			else if (change is PropertyChange)
			{
				var c = (PropertyChange)change;

				if (controllables.Contains(c.Object))
				{
					return true;
				}
				else if (c.Property.Visibility == PropertyVisibility.Friendly)
				{
					// xxx should check if the object is friendly
					// return false for now, as all friendlies are controllables, thus we will still see it
					// because the check above will return true to that controllable
					return false;
				}
				else if (c.Property.Visibility == PropertyVisibility.Public)
				{
					ServerGameObject ob = (ServerGameObject)c.Object;

					return controllables.Any(l => l.Sees(ob.Environment, ob.Location));
				}
				else
				{
					throw new Exception();
				}
			}
			else if (change is ActionStartedChange)
			{
				var c = (ActionStartedChange)change;
				return controllables.Contains(c.Object);
			}
			else if (change is ActionProgressChange)
			{
				var c = (ActionProgressChange)change;
				return controllables.Contains(c.Object);
			}
			else if (change is TickStartChange || change is ObjectDestructedChange)
			{
				return true;
			}
			else if (change is ObjectCreatedChange)
			{
				return false;
			}
			else
			{
				throw new Exception();
			}
		}

		static ServerMessage ObjectToMessage(BaseGameObject revealedOb)
		{
			var msg = new ObjectDataMessage() { ObjectData = revealedOb.Serialize() };
			return msg;
		}
	}
}
