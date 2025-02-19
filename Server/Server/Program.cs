﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Diagnostics;


namespace Dwarrowdelf.Server
{
	static class ServerLauncher
	{
		static void Main(string[] args)
		{
			Thread.CurrentThread.Name = "SMain";

			var path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "save");

			if (!System.IO.Directory.Exists(path))
				System.IO.Directory.CreateDirectory(path);

			var gameDir = path;

			bool cleanSaves = true;

			SaveManager saveManager = new SaveManager(gameDir);

			Guid save = Guid.Empty;

			if (cleanSaves)
				saveManager.DeleteAll();
			else
				save = saveManager.GetLatestSaveFile();

			var gf = new GameFactory();
			IGame game;

			if (save == Guid.Empty)
				game = gf.CreateGame(gameDir,
					new GameOptions()
					{
						Mode = GameMode.Fortress,
						Map = GameMap.Fortress,
						MapSize = new IntSize3(128, 128, 32),
						TickMethod = WorldTickMethod.Simultaneous
					});
			else
				game = gf.LoadGame(gameDir, save);

			var keyThread = new Thread(KeyMain);
			keyThread.Start(game);

			game.Run(null);
		}

		static string GetLatestSaveFile(string gameDir)
		{
			var files = Directory.EnumerateFiles(gameDir);
			var list = new System.Collections.Generic.List<string>(files);
			list.Sort();
			var last = list[list.Count - 1];
			return Path.GetFileName(last);
		}

		static void KeyMain(object _game)
		{
			var game = (IGame)_game;

			Console.WriteLine("q - quit, s - signal, p - enable singlestep, r - disable singlestep, . - step");

			while (true)
			{
				var key = Console.ReadKey(true).Key;

				switch (key)
				{
					case ConsoleKey.Q:
						Console.WriteLine("Quit");
						game.Stop();
						return;

					case ConsoleKey.S:
						Console.WriteLine("Signal");
						game.Signal();
						break;

					case ConsoleKey.P:
						//game.World.EnableSingleStep();
						break;

					case ConsoleKey.R:
						//game.World.DisableSingleStep();
						break;

					case ConsoleKey.OemPeriod:
						//game.World.SingleStep();
						break;

					default:
						Console.WriteLine("Unknown key");
						break;
				}
			}
		}
	}
}