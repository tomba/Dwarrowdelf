﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using Dwarrowdelf;
using System.Diagnostics;

namespace TerrainGenTest
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			var listener = new MMLogTraceListener();
			System.Diagnostics.Trace.Listeners.Clear();
			System.Diagnostics.Trace.Listeners.Add(listener);
		}
	}
}
