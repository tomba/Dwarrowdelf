﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace Dwarrowdelf.Client.TileControlD2D
{
	public interface IBitmapGenerator
	{
		BitmapSource GetBitmap(SymbolID symbolID, GameColor color);
		int NumDistinctBitmaps { get; }
		int TileSize { get; set; }
	}
}
