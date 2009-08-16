﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MyGame;

namespace MyArea
{
	public class Area : IArea
	{
		public void InitializeWorld(World world)
		{
			// Add a monster
			var monster = new Living(world);
			monster.SymbolID = 4;
			monster.Name = "monsu";
			if (monster.MoveTo(world.Map, new IntPoint(6, 6)) == false)
				throw new Exception();
			var monsterAI = new MonsterActor(monster);
			monster.Actor = monsterAI;

			// Add an item
			var item = new ItemObject(world);
			item.SymbolID = 5;
			item.Name = "testi-itemi";
			item.MoveTo(world.Map, new IntPoint(3, 0));
		}
	}
}
