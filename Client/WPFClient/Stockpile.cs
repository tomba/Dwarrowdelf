﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dwarrowdelf.Jobs;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;

namespace Dwarrowdelf.Client
{
	[Flags]
	enum StockpileType
	{
		None = 0,
		Logs = 1 << 0,
		Gems = 1 << 1,
		Rocks = 1 << 2,
		Metals = 1 << 3,
		Furniture = 1 << 4,
	}

	// XXX should be configurable for classes, subclasses and certain items...

	class Stockpile : IDrawableElement, IJobSource
	{
		public Environment Environment { get; private set; }
		IntCuboid IDrawableElement.Area { get { return this.Area.ToCuboid(); } }

		public IntRect3D Area { get; private set; }

		public StockpileType StockpileType { get; private set; }

		List<StoreToStockpileJob> m_jobs = new List<StoreToStockpileJob>();

		FrameworkElement m_element;
		public FrameworkElement Element { get { return m_element; } }

		public Stockpile(Environment environment, IntRect3D area, StockpileType stockpileType)
		{
			this.Environment = environment;
			this.Area = area;
			this.StockpileType = stockpileType;

			var rect = new Rectangle();
			rect.Stroke = Brushes.Gray;
			rect.StrokeThickness = 0.1;
			rect.Width = area.Width;
			rect.Height = area.Height;
			m_element = rect;

			this.Environment.World.JobManager.AddJobSource(this);
		}

		public void Destruct()
		{
			this.Environment.World.JobManager.RemoveJobSource(this);

			foreach (var job in m_jobs)
			{
				job.Item.ReservedBy = null;
				job.StateChanged -= OnJobStateChanged;
				GameData.Data.Jobs.Remove(job);
			}

			m_jobs = null;
		}

		bool IJobSource.HasWork
		{
			get
			{
				return true; // XXX
			}
		}

		IAssignment IJobSource.GetJob(ILiving living)
		{
			var obs = this.Environment.GetContents()
				.OfType<ItemObject>()
				.Where(o => o.ReservedBy == null)
				.Where(o => Match(o))
				.Where(o => { var sp = this.Environment.GetStockpileAt(o.Location); return !(sp != null && sp.Match(o)); }); // XXX

			foreach (var ob in obs)
			{
				var job = new StoreToStockpileJob(this, ob);

				var jobState = job.Assign(living);

				switch (jobState)
				{
					case JobState.Ok:
						job.Item.ReservedBy = this;
						job.StateChanged += OnJobStateChanged;
						m_jobs.Add(job);

						GameData.Data.Jobs.Add(job);

						return job;

					case JobState.Done:
						throw new Exception();

					case JobState.Abort:
					case JobState.Fail:
						break;

					default:
						throw new Exception();
				}
			}

			return null;
		}

		void OnJobStateChanged(IJob job, JobState state)
		{
			var j = (StoreToStockpileJob)job;

			if (state == JobState.Ok)
			{
				throw new Exception();
			}

			j.Item.ReservedBy = null;
			job.StateChanged -= OnJobStateChanged;
			m_jobs.Remove(j);

			GameData.Data.Jobs.Remove(j);
		}

		public IntPoint3D FindEmptyLocation(out bool ok)
		{
			var env = this.Environment;


			for (int i = 0; i < 10; ++i)
			{
				var loc = this.Area.Range().FirstOrDefault(p => env.GetContents(p).OfType<ItemObject>().Count() == i);

				if (loc != new IntPoint3D())
				{
					ok = true;
					return loc;
				}
			}

			ok = false;
			return new IntPoint3D();
		}

		bool Match(ItemObject item)
		{
			StockpileType types = this.StockpileType;

			if ((types & StockpileType.Logs) != 0 && item.ItemClass == ItemClass.RawMaterial && item.MaterialClass == MaterialClass.Wood)
				return true;

			if ((types & StockpileType.Gems) != 0 && item.ItemClass == ItemClass.Gem && item.MaterialClass == MaterialClass.Gem)
				return true;

			if ((types & StockpileType.Rocks) != 0 && item.ItemClass == ItemClass.RawMaterial && item.MaterialClass == MaterialClass.Rock)
				return true;

			if ((types & StockpileType.Metals) != 0 && item.ItemClass == ItemClass.RawMaterial && item.MaterialClass == MaterialClass.Metal)
				return true;

			if ((types & StockpileType.Furniture) != 0 && item.ItemClass == ItemClass.Furniture)
				return true;

			return false;
		}

		public override string ToString()
		{
			return String.Format("Stockpile({0})", this.StockpileType);
		}
	}
}
