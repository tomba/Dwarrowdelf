﻿using System;
using SharpDX.Direct3D11;

namespace Dwarrowdelf.Client.TileControl
{
	public interface IScene : IDisposable
	{
		void Attach(ISceneHost host);
		void Detach();
		void Update(TimeSpan timeSpan);
		void Render();
	}

	public interface ISceneHost : IDisposable
	{
		Device Device { get; }
	}
}
