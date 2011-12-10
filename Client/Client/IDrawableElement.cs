﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dwarrowdelf.Client
{
	interface IDrawableElement
	{
		EnvironmentObject Environment { get; }
		IntCuboid Area { get; }
		string Description { get; }
	}
}
