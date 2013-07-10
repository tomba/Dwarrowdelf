﻿using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using System.Runtime.InteropServices;

namespace Dwarrowdelf.Client.TileControl
{
	class SingleQuad11PS : Component
	{
		Device m_device;
		PixelShader m_pixelShader;

		[StructLayout(LayoutKind.Sequential, Size = 16)]
		struct ShaderData
		{
			public int SimpleTint;
		}

		[StructLayout(LayoutKind.Sequential, Size = 32)]
		struct ShaderDataPerFrame
		{
			public Vector2 ColRow;	/* columns, rows */
			public Vector2 RenderOffset;
			public float TileSize;
		}

		ShaderDataPerFrame m_shaderDataPerFrame;
		Buffer m_shaderDataBufferPerFrame;


		Buffer m_tileBuffer;
		ShaderResourceView m_tileBufferView;

		ShaderResourceView m_tileTextureView;

		public SingleQuad11PS(Device device)
		{
			m_device = device;

			var ass = System.Reflection.Assembly.GetCallingAssembly();

			// fxc /T ps_4_0 /E PS /Fo SingleQuad11.pso SingleQuad11.ps

			using (var stream = ass.GetManifestResourceStream("Dwarrowdelf.Client.TileControl.SingleQuad11PS.hlslo"))
			{
				var bytecode = ShaderBytecode.FromStream(stream);
				Create(bytecode);
			}
		}

		void Create(ShaderBytecode bytecode)
		{
			var context = m_device.ImmediateContext;

			m_pixelShader = ToDispose(new PixelShader(m_device, bytecode));
			context.PixelShader.Set(m_pixelShader);

			/* Constant buffer */
			var shaderDataBuffer = ToDispose(new Buffer(m_device, Utilities.SizeOf<ShaderData>(),
				ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0));

			context.PixelShader.SetConstantBuffer(0, shaderDataBuffer);

			ShaderData shaderData = new ShaderData()
			{
				SimpleTint = 1,
			};

			context.UpdateSubresource(ref shaderData, shaderDataBuffer);

			/* Constant buffer per frame */
			m_shaderDataBufferPerFrame = ToDispose(new Buffer(m_device, Utilities.SizeOf<ShaderDataPerFrame>(),
				ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0));

			context.PixelShader.SetConstantBuffer(1, m_shaderDataBufferPerFrame);

			/* color buffer */
			var colorBuffer = ToDispose(Helpers11.CreateGameColorBuffer(m_device));

			var colorBufferView = ToDispose(new ShaderResourceView(m_device, colorBuffer, new ShaderResourceViewDescription()
			{
				Format = SharpDX.DXGI.Format.R32_UInt,
				Dimension = ShaderResourceViewDimension.Buffer,
				Buffer = new ShaderResourceViewDescription.BufferResource()
				{
					ElementWidth = colorBuffer.Description.SizeInBytes / sizeof(uint),
					ElementOffset = 0,
				},
			}));

			context.PixelShader.SetShaderResource(1, colorBufferView);

			/* Texture sampler */
			var sampler = ToDispose(new SamplerState(m_device, new SamplerStateDescription()
			{
				/* Use point filtering as linear filtering causes artifacts */
				Filter = Filter.MinMagMipPoint,
				AddressU = TextureAddressMode.Wrap,
				AddressV = TextureAddressMode.Wrap,
				AddressW = TextureAddressMode.Wrap,
				BorderColor = new Color4(0),
				ComparisonFunction = Comparison.Never,
				MipLodBias = 0,
				MinimumLod = 0,
				MaximumLod = float.MaxValue,
			}));

			context.PixelShader.SetSampler(0, sampler);
		}

		public void SetupTileBuffer(IntSize2 renderTargetSize)
		{
			RemoveAndDispose(ref m_tileBuffer);
			RemoveAndDispose(ref m_tileBufferView);

			const int minTileSize = 2;
			var tileBufferWidth = (int)System.Math.Ceiling((double)renderTargetSize.Width / minTileSize + 1) | 1;
			var tileBufferHeight = (int)System.Math.Ceiling((double)renderTargetSize.Height / minTileSize + 1) | 1;

			m_tileBuffer = ToDispose(new SharpDX.Direct3D11.Buffer(m_device, new BufferDescription()
			{
				BindFlags = BindFlags.ShaderResource,
				CpuAccessFlags = CpuAccessFlags.Write,
				OptionFlags = ResourceOptionFlags.BufferStructured,
				SizeInBytes = tileBufferWidth * tileBufferHeight * Marshal.SizeOf(typeof(RenderTile)),
				StructureByteStride = Marshal.SizeOf(typeof(RenderTile)),
				Usage = ResourceUsage.Dynamic,
			}));

			m_tileBufferView = ToDispose(new ShaderResourceView(m_device, m_tileBuffer, new ShaderResourceViewDescription()
			{
				Format = SharpDX.DXGI.Format.Unknown,
				Dimension = ShaderResourceViewDimension.Buffer,
				Buffer = new ShaderResourceViewDescription.BufferResource()
				{
					ElementWidth = tileBufferWidth * tileBufferHeight,
					ElementOffset = 0,
				},
			}));

			var context = m_device.ImmediateContext;
			context.PixelShader.SetShaderResource(2, m_tileBufferView);
		}

		public void SetTileTextures(Texture2D textureArray)
		{
			RemoveAndDispose(ref m_tileTextureView);

			m_tileTextureView = ToDispose(new ShaderResourceView(m_device, textureArray, new ShaderResourceViewDescription()
			{
				Format = textureArray.Description.Format,
				Dimension = ShaderResourceViewDimension.Texture2DArray,
				Texture2DArray = new ShaderResourceViewDescription.Texture2DArrayResource()
				{
					MipLevels = textureArray.Description.MipLevels,
					MostDetailedMip = 0,
					ArraySize = textureArray.Description.ArraySize,
				},
			}));

			var context = m_device.ImmediateContext;
			context.PixelShader.SetShaderResource(0, m_tileTextureView);
		}

		public void SendMapData(RenderTile[] mapData, int columns, int rows)
		{
			m_shaderDataPerFrame.ColRow = new Vector2(columns, rows);

			DataStream stream;
			var box = m_device.ImmediateContext.MapSubresource(m_tileBuffer, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out stream);
			stream.WriteRange(mapData, 0, columns * rows);
			m_device.ImmediateContext.UnmapSubresource(m_tileBuffer, 0);
			stream.Dispose();
		}

		public void Setup(float tileSize, Vector2 renderOffset)
		{
			var context = m_device.ImmediateContext;

			m_shaderDataPerFrame.TileSize = tileSize;
			m_shaderDataPerFrame.RenderOffset = renderOffset;

			context.UpdateSubresource(ref m_shaderDataPerFrame, m_shaderDataBufferPerFrame);
		}
	}
}
