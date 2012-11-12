﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Dwarrowdelf.Client.UI
{
	sealed class MainWindowCommandHandler
	{
		MainWindow m_mainWindow;

		public MainWindowCommandHandler(MainWindow mainWindow)
		{
			m_mainWindow = mainWindow;

			m_mainWindow.CommandBindings.Add(new CommandBinding(ClientCommands.AutoAdvanceTurnCommand, AutoAdvanceTurnHandler));
			m_mainWindow.CommandBindings.Add(new CommandBinding(ClientCommands.OpenConsoleCommand, OpenConsoleHandler));
			m_mainWindow.CommandBindings.Add(new CommandBinding(ClientCommands.OpenFocusDebugCommand, OpenFocusDebugHandler));
		}

		public void AddAdventureCommands()
		{
			m_mainWindow.CommandBindings.Add(new CommandBinding(ClientCommands.DropItemCommand, DropItemHandler));
			m_mainWindow.InputBindings.Add(new InputBinding(ClientCommands.DropItemCommand, new GameKeyGesture(Key.D)));
		}

		void DropItemHandler(object sender, ExecutedRoutedEventArgs e)
		{
			var living = m_mainWindow.FocusedObject;

			if (living == null)
				return;

			var dlg = new InventoryItemSelectorDialog();
			dlg.Owner = m_mainWindow;
			dlg.DataContext = living;
			dlg.Title = "Drop Item";
			var ret = dlg.ShowDialog();
			if (ret.HasValue && ret.Value == true)
			{
				var ob = dlg.SelectedItem;

				var action = new DropItemAction(ob);
				action.MagicNumber = 1;
				living.RequestAction(action);
			}

			e.Handled = true;
		}

		void AutoAdvanceTurnHandler(object sender, ExecutedRoutedEventArgs e)
		{
			GameData.Data.IsAutoAdvanceTurn = !GameData.Data.IsAutoAdvanceTurn;
		}

		void OpenConsoleHandler(object sender, ExecutedRoutedEventArgs e)
		{
			var dialog = new ConsoleDialog();
			dialog.Owner = m_mainWindow;
			dialog.Show();
		}

		void OpenFocusDebugHandler(object sender, ExecutedRoutedEventArgs e)
		{
			var dialog = new Dwarrowdelf.Client.UI.Windows.FocusDebugWindow();
			dialog.Owner = m_mainWindow;
			dialog.Show();
		}
	}
}
