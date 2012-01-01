﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dwarrowdelf.Client.UI
{
	sealed partial class TreeViewTemplateDictionary
	{
		void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			var menuItem = (MenuItem)sender;
			var cm = (ContextMenu)menuItem.Parent;
			DependencyObject obj = cm.PlacementTarget;

			while (obj != null && !(obj is TreeViewItem))
				obj = VisualTreeHelper.GetParent(obj);

			var tvi = obj as TreeViewItem;
			var baseObject = tvi.Header as BaseObject;

			if (baseObject == null)
				return;

			var tag = (string)menuItem.Tag;

			switch (tag)
			{
				case "Goto":
					{
						var movable = baseObject as MovableObject;
						if (movable != null && movable.Environment != null)
						{
							App.MainWindow.MapControl.ScrollTo(movable.Environment, movable.Location);
							return;
						}

						var building = baseObject as BuildingObject;
						if (building != null && building.Environment != null)
						{
							App.MainWindow.MapControl.ScrollTo(building.Environment, building.Area.Center);
							return;
						}
					}
					break;

				case "Info":
					{
						var dlg = new ObjectEditDialog();
						dlg.DataContext = baseObject;
						dlg.Owner = App.MainWindow;
						dlg.Show();

					}
					break;

				case "Control":
					{
						var living = baseObject as LivingObject;

						if (living == null)
							return;

						var wnd = new LivingControlWindow();
						wnd.DataContext = living;
						wnd.Owner = App.MainWindow;
						wnd.Show();
					}
					break;

				default:
					throw new Exception();
			}
		}
	}
}
