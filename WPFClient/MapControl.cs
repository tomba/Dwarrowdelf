﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MyGame.Client
{
	class MapControl : MapControlBase, INotifyPropertyChanged
	{
		World m_world;
		SymbolBitmapCache m_bitmapCache;

		Environment m_env;
		int m_z;

		TileInfo m_selectedTileInfo;
		public HoverTileInfo HoverTileInfo { get; private set; }


		public MapControl()
		{
			this.HoverTileInfo = new HoverTileInfo();

			base.SelectionChanged += OnSelectionChanged;

			var dpd = DependencyPropertyDescriptor.FromProperty(MapControlBase.TileSizeProperty,
				typeof(MapControlBase));
			dpd.AddValueChanged(this, OnTileSizeChanged);

			var dpd2 = DependencyPropertyDescriptor.FromProperty(MapControlBase.CenterPosProperty,
				typeof(MapControlBase));
			dpd2.AddValueChanged(this, OnCenterPosChanged);
		}

		void OnTileSizeChanged(object ob, EventArgs e)
		{
			if (m_bitmapCache != null)
				m_bitmapCache.TileSize = this.TileSize;
		}

		void OnCenterPosChanged(object ob, EventArgs e)
		{
			UpdateHoverTileInfo(Mouse.GetPosition(this));
		}

		protected override UIElement CreateTile()
		{
			return new MapControlTile();
		}

		protected override void UpdateTile(UIElement _tile, IntPoint _ml)
		{
			BitmapSource bmp = null;
			MapControlTile tile = (MapControlTile)_tile;
			bool lit = false;
			IntPoint3D ml = new IntPoint3D(_ml.X, _ml.Y, this.Z);

			if (this.Environment == null)
			{
				tile.Bitmap = null;
				tile.ObjectBitmap = null;
				return;
			}

			if (GameData.Data.IsSeeAll)
				lit = true;
			else
				lit = TileVisible(ml);

			bmp = GetBitmap(ml, lit);
			tile.Bitmap = bmp;

			if (GameData.Data.DisableLOS)
				lit = true; // lit always so we see what server sends

			if (lit)
				bmp = GetObjectBitmap(ml, lit);
			else
				bmp = null;
			tile.ObjectBitmap = bmp;
		}

		bool TileVisible(IntPoint3D ml)
		{
			if (this.Environment.VisibilityMode == VisibilityMode.AllVisible)
				return true;

			if (this.Environment.GetInterior(ml).ID == InteriorID.Undefined)
				return false;

			var controllables = this.Environment.World.Controllables;

			if (this.Environment.VisibilityMode == VisibilityMode.LOS)
			{
				foreach (var l in controllables)
				{
					if (l.Environment != this.Environment || l.Location.Z != this.Z)
						continue;

					IntPoint vp = new IntPoint(ml.X - l.Location.X, ml.Y - l.Location.Y);

					if (Math.Abs(vp.X) <= l.VisionRange && Math.Abs(vp.Y) <= l.VisionRange &&
						l.VisionMap[vp] == true)
						return true;
				}
			}
			else if (this.Environment.VisibilityMode == VisibilityMode.SimpleFOV)
			{
				foreach (var l in controllables)
				{
					if (l.Environment != this.Environment || l.Location.Z != this.Z)
						continue;

					IntPoint vp = new IntPoint(ml.X - l.Location.X, ml.Y - l.Location.Y);

					if (Math.Abs(vp.X) <= l.VisionRange && Math.Abs(vp.Y) <= l.VisionRange)
						return true;
				}
			}
			else
			{
				throw new Exception();
			}

			return false;
		}

		BitmapSource GetBitmap(IntPoint3D ml, bool lit)
		{
			int id;
			Color c;

			var iInfo = this.Environment.GetInterior(ml);

			if (iInfo.ID != InteriorID.Empty)
			{
				var symbol = this.Environment.World.AreaData.Symbols.Single(s => s.Name == iInfo.Name);
				id = symbol.ID;
				c = Colors.Black;
			}
			else
			{
				var fInfo = this.Environment.GetFloor(ml);
				var symbol = this.Environment.World.AreaData.Symbols.Single(s => s.Name == fInfo.Name);
				id = symbol.ID;
				c = Colors.Black;
			}

			return m_bitmapCache.GetBitmap(id, c, !lit);
		}

		BitmapSource GetObjectBitmap(IntPoint3D ml, bool lit)
		{
			IList<ClientGameObject> obs = this.Environment.GetContents(ml);
			if (obs != null && obs.Count > 0)
			{
				int id = obs[0].SymbolID;
				Color c = obs[0].Color;
				return m_bitmapCache.GetBitmap(id, c, !lit);
			}
			else
				return null;
		}

		public Environment Environment
		{
			get { return m_env; }

			set
			{
				if (m_env == value)
					return;

				if (m_env != null)
					m_env.MapChanged -= MapChangedCallback;

				m_env = value;

				if (m_env != null)
				{
					m_env.MapChanged += MapChangedCallback;

					if (m_world != m_env.World)
					{
						m_world = m_env.World;
						m_bitmapCache = new SymbolBitmapCache(m_world.SymbolDrawingCache, this.TileSize);
					}
				}
				else
				{
					m_world = null;
					m_bitmapCache = null;
				}

				InvalidateTiles();

				Notify("Environment");
			}
		}

		public int Z
		{
			get { return m_z; }

			set
			{
				if (m_z == value)
					return;

				m_z = value;
				InvalidateTiles();
				Notify("Z");
				UpdateHoverTileInfo(Mouse.GetPosition(this));
			}
		}


		void MapChangedCallback(IntPoint3D l)
		{
			InvalidateTiles();
		}

		public TileInfo SelectedTileInfo
		{
			get { return m_selectedTileInfo; }
			set
			{
				if (m_selectedTileInfo == value)
					return;

				m_selectedTileInfo = value;

				Notify("SelectedTileInfo");
			}
		}

		void OnSelectionChanged()
		{
			IntRect sel = this.SelectionRect;

			if (sel.Width != 1 || sel.Height != 1)
			{
				if (this.SelectedTileInfo != null)
					this.SelectedTileInfo.StopObserve();
				this.SelectedTileInfo = null;
				return;
			}

			if (this.SelectedTileInfo == null)
			{
				this.SelectedTileInfo = new TileInfo(this.Environment, new IntPoint3D(sel.TopLeft, this.Z));
			}
			else
			{
				this.SelectedTileInfo.Environment = this.Environment;
				this.SelectedTileInfo.Location = new IntPoint3D(sel.TopLeft, this.Z);
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			UpdateHoverTileInfo(e.GetPosition(this));
		}

		void UpdateHoverTileInfo(Point mousePos)
		{
			IntPoint ml = ScreenPointToMapLocation(mousePos);
			var p = new IntPoint3D(ml, m_z);

			if (p != this.HoverTileInfo.Location)
			{
				this.HoverTileInfo.Location = p;
				Notify("HoverTileInfo");
			}
		}


		void Notify(string name)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(name));
		}

		#region INotifyPropertyChanged Members
		public event PropertyChangedEventHandler PropertyChanged;
		#endregion
	}

	class HoverTileInfo : INotifyPropertyChanged
	{
		public IntPoint3D Location { get; set; }

		void Notify(string name)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(name));
		}

		#region INotifyPropertyChanged Members
		public event PropertyChangedEventHandler PropertyChanged;
		#endregion
	}

	class TileInfo : INotifyPropertyChanged
	{
		Environment m_env;
		IntPoint3D m_location;

		public TileInfo()
		{
		}

		public TileInfo(Environment mapLevel, IntPoint3D location)
		{
			m_env = mapLevel;
			m_location = location;
			if (m_env != null)
				m_env.MapChanged += MapChanged;
		}

		public void StopObserve()
		{
			if (m_env != null)
				m_env.MapChanged -= MapChanged;
		}

		void NotifyTileChanges()
		{
			Notify("Interior");
			Notify("Floor");
			Notify("FloorMaterial");
			Notify("InteriorMaterial");
			Notify("Objects");
			Notify("Building");
		}

		void MapChanged(IntPoint3D l)
		{
			if (l == m_location)
				NotifyTileChanges();
		}

		public Environment Environment
		{
			get { return m_env; }
			set
			{
				if (m_env != null)
					m_env.MapChanged -= MapChanged;

				m_env = value;

				if (m_env != null)
					m_env.MapChanged += MapChanged;

				Notify("Environment");
				NotifyTileChanges();
			}
		}

		public IntPoint3D Location
		{
			get { return m_location; }
			set
			{
				m_location = value;
				Notify("Location");
				NotifyTileChanges();
			}
		}

		public InteriorInfo Interior
		{
			get
			{
				if (m_env == null)
					return null;
				return m_env.GetInterior(m_location);
			}
		}

		public MaterialInfo InteriorMaterial
		{
			get
			{
				if (m_env == null)
					return null;
				return m_env.GetInteriorMaterial(m_location);
			}
		}

		public MaterialInfo FloorMaterial
		{
			get
			{
				if (m_env == null)
					return null;
				return m_env.GetFloorMaterial(m_location);
			}
		}

		public FloorInfo Floor
		{
			get
			{
				if (m_env == null)
					return null;
				return m_env.GetFloor(m_location);
			}
		}

		public IList<ClientGameObject> Objects
		{
			get
			{
				if (m_env == null)
					return null;
				return m_env.GetContents(m_location);
			}
		}

		public BuildingData Building
		{
			get
			{
				if (m_env == null)
					return null;
				return m_env.GetBuildingAt(m_location);
			}
		}

		void Notify(string name)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(name));
		}

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion
	}

	class MapControlTile : UIElement
	{
		public MapControlTile()
		{
			this.IsHitTestVisible = false;
		}

		public static readonly DependencyProperty BitmapProperty = DependencyProperty.Register(
			"Bitmap", typeof(BitmapSource), typeof(MapControlTile),
			new PropertyMetadata(null, ValueChangedCallback));

		public BitmapSource Bitmap
		{
			get { return (BitmapSource)GetValue(BitmapProperty); }
			set { SetValue(BitmapProperty, value); }
		}

		public static readonly DependencyProperty ObjectBitmapProperty = DependencyProperty.Register(
			"ObjectBitmap", typeof(BitmapSource), typeof(MapControlTile),
			new PropertyMetadata(null, ValueChangedCallback));

		public BitmapSource ObjectBitmap
		{
			get { return (BitmapSource)GetValue(ObjectBitmapProperty); }
			set { SetValue(ObjectBitmapProperty, value); }
		}

		static void ValueChangedCallback(DependencyObject ob, DependencyPropertyChangedEventArgs e)
		{
			((MapControlTile)ob).InvalidateVisual();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (this.Bitmap != null)
				drawingContext.DrawImage(this.Bitmap, new Rect(this.RenderSize));

			if (this.ObjectBitmap != null)
				drawingContext.DrawImage(this.ObjectBitmap, new Rect(this.RenderSize));
		}
	}
}
