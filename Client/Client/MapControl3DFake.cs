using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Dwarrowdelf.Client
{
    	public enum CameraControlMode
	{
		None,
		Rts,
		Fps,
	}

    public class MapControlConfig : INotifyPropertyChanged
    {
        private MapControlPickMode m_pickMode;
        private CameraControlMode m_cameraControlMode;

        public MapControlPickMode PickMode
        {
            get { return m_pickMode; }
            set { m_pickMode = value; Notify("PickMode"); }
        }

        public CameraControlMode CameraControlMode
        {
            get { return m_cameraControlMode; }
            set { m_cameraControlMode = value; Notify("CameraControlMode"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Notify(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
    }

    public enum MapControlPickMode
    {
        Underground,
        AboveGround,
        Constant,
    }

    public class MapControl3D : UserControl
    {
        public MapControlConfig Config { get; private set; }

        public MapControl3D()
        {
            this.Config = new MapControlConfig();
            this.HoverTileView = new TileAreaView();
            this.SelectionTileAreaView = new TileAreaView();
        }
        public void Dispose()
        {

        }

        public EnvironmentObject Environment
        {
            get;
            set;
        }


        public void OpenDebugWindow()
        {
            // Do nothing
        }

        public Rect GetPlacementRect(IntVector3 ml)
        {
            // Return a default Rect
            return new Rect();
        }

        public void CameraLookAt(MovableObject ob)
        {
        }

        public void CameraLookAt(EnvironmentObject env, IntVector3 p)
        {
            // Do nothing
        }

        public void CameraMoveTo(MovableObject ob)
        { 
        }

        public void CameraMoveTo(EnvironmentObject env, IntVector3 p)
        {
            // Do nothing
        }

        public MapSelectionMode SelectionMode { get; set; }

        public MapSelection Selection { get; set; }

        public TileAreaView HoverTileView { get; private set; }

        public TileAreaView SelectionTileAreaView { get; private set; }

        public event Action<MapSelection> GotSelection;
    }

    public class TileAreaView
    {
        public void ClearTarget()
        {
            // Do nothing
        }

        public void SetTarget(EnvironmentObject env, IntVector3 ml)
        {
            // Do nothing
        }

        public void SetTarget(EnvironmentObject env, IntGrid3 selectionBox)
        {
            // Do nothing
        }
    }
}
