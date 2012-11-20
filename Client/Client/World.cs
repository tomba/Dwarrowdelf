﻿#if DEBUG
#define TRACK_DESTRUCTED_OBJECTS
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Dwarrowdelf.Client
{
	sealed class World : IWorld, INotifyPropertyChanged
	{
		BaseGameObjectCollection m_objects;
		public ReadOnlyBaseGameObjectCollection Objects { get; private set; }

		BaseGameObjectCollection m_rootObjects;
		public ReadOnlyBaseGameObjectCollection RootObjects { get; private set; }

		EnvironmentCollection m_environments;
		public ReadOnlyEnvironmentCollection Environments { get; private set; }

		public LivingCollection Controllables { get; private set; }

		public Dwarrowdelf.Jobs.JobManager JobManager { get; private set; }

		// perhaps this is not needed in client side
		public int UserID { get; set; }

		public LivingVisionMode LivingVisionMode { get; private set; }

		public GameMode GameMode { get; private set; }

#if TRACK_DESTRUCTED_OBJECTS
		BaseGameObjectCollection m_destructedObjects = new BaseGameObjectCollection();
#endif

		public World(GameMode gameMode, LivingVisionMode livingVisionMode, int tick)
		{
			this.GameMode = gameMode;
			this.LivingVisionMode = livingVisionMode;
			this.TickNumber = tick;

			m_objects = new BaseGameObjectCollection();
			this.Objects = new ReadOnlyBaseGameObjectCollection(m_objects);

			m_rootObjects = new BaseGameObjectCollection();
			this.RootObjects = new ReadOnlyBaseGameObjectCollection(m_rootObjects);

			m_environments = new EnvironmentCollection();
			this.Environments = new ReadOnlyEnvironmentCollection(m_environments);

			this.Controllables = new LivingCollection();

			this.JobManager = new Dwarrowdelf.Jobs.JobManager(this);
		}

		internal void AddEnvironment(EnvironmentObject env)
		{
			m_environments.Add(env);
		}

		int m_tickNumber;
		public int TickNumber
		{
			get { return m_tickNumber; }
			private set { m_tickNumber = value; MyTraceContext.ThreadTraceContext.Tick = this.TickNumber; Notify("TickNumber"); }
		}

		// XXX this could perhaps be removed
		Random m_random = new Random();
		public Random Random { get { return m_random; } }

		// LivingID, AnyObjectID or NullIObjectID
		public ObjectID CurrentLivingID { get; private set; }

		public event Action TickStarting;
		public event Action<ObjectID> TurnStarted;
		public event Action TurnEnded;

		public void HandleChange(TickStartChangeData change)
		{
			this.TickNumber = change.TickNumber;

			if (TickStarting != null)
				TickStarting();

			GameData.Data.AddTickGameEvent();
		}

		public void HandleChange(TurnStartChangeData change)
		{
			Debug.Assert(this.CurrentLivingID == ObjectID.NullObjectID);
			Debug.Assert(change.LivingID != ObjectID.NullObjectID);

			this.CurrentLivingID = change.LivingID;

			if (TurnStarted != null)
				TurnStarted(change.LivingID);
		}

		public void HandleChange(TurnEndChangeData change)
		{
			this.CurrentLivingID = ObjectID.NullObjectID;

			if (TurnEnded != null)
				TurnEnded();
		}

		BaseObject ConstructObject(ObjectID objectID)
		{
			switch (objectID.ObjectType)
			{
				case ObjectType.Environment:
					return new EnvironmentObject(this, objectID);

				case ObjectType.Living:
					return new LivingObject(this, objectID);

				case ObjectType.Item:
					return new ItemObject(this, objectID);

				case ObjectType.Building:
					return new BuildingObject(this, objectID);

				default:
					throw new Exception();
			}
		}

		public BaseObject CreateObject(ObjectID objectID)
		{
			if (objectID == ObjectID.NullObjectID || objectID == ObjectID.AnyObjectID)
				throw new ArgumentException();

			if (m_objects.Contains(objectID))
				throw new Exception();

			var ob = ConstructObject(objectID);

			m_objects.Add(ob);

			m_rootObjects.Add(ob);
			var movable = ob as MovableObject;
			if (movable != null)
				movable.PropertyChanged += MovablePropertyChanged;

			ob.Destructed += ObjectDestructed;

			return ob;
		}

		void MovablePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != "Parent")
				return;

			var ob = (MovableObject)sender;
			if (ob.Parent == null)
			{
				if (m_rootObjects.Contains(ob))
					return;

				m_rootObjects.Add(ob);
			}
			else
			{
				m_rootObjects.Remove(ob);
			}
		}

		void ObjectDestructed(BaseObject ob)
		{
			ob.Destructed -= ObjectDestructed;

			var movable = ob as MovableObject;
			if (movable != null)
				movable.PropertyChanged -= MovablePropertyChanged;

			m_rootObjects.Remove(ob);

			if (m_objects.Remove(ob) == false)
				throw new Exception();

#if TRACK_DESTRUCTED_OBJECTS
			m_destructedObjects.Add(ob);
#endif
		}

		public BaseObject FindObject(ObjectID objectID)
		{
			if (objectID == ObjectID.NullObjectID || objectID == ObjectID.AnyObjectID)
				throw new ArgumentException();

			if (m_objects.Contains(objectID))
				return m_objects[objectID];
			else
				return null;
		}

		public T FindObject<T>(ObjectID objectID) where T : BaseObject
		{
			var ob = FindObject(objectID);

			if (ob == null)
				return null;

			return (T)ob;
		}

		public BaseObject GetObject(ObjectID objectID)
		{
			var ob = FindObject(objectID);

			if (ob == null)
			{
#if TRACK_DESTRUCTED_OBJECTS
				if (m_destructedObjects.Contains(objectID))
				{
					ob = m_destructedObjects[objectID];
					throw new Exception(String.Format("Getting destructed object {0}", ob));
				}
#endif
				throw new Exception();
			}

			return ob;
		}

		public T GetObject<T>(ObjectID objectID) where T : BaseObject
		{
			return (T)GetObject(objectID);
		}

		public BaseObject FindOrCreateObject(ObjectID objectID)
		{
			var ob = FindObject(objectID);

			if (ob != null)
				return ob;

			return CreateObject(objectID);
		}

		public T FindOrCreateObject<T>(ObjectID objectID) where T : BaseObject
		{
			return (T)FindOrCreateObject(objectID);
		}

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		void Notify(string info)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(info));
		}
	}
}
