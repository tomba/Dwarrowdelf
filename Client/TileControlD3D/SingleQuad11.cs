﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using SlimDX.Windows;
using SlimDX.D3DCompiler;
using Device = SlimDX.Direct3D11.Device;
using System.Runtime.InteropServices;
using Dwarrowdelf;

namespace Dwarrowdelf.Client.TileControl
{
	class SingleQuad11 : IDisposable
	{
		[StructLayout(LayoutKind.Sequential)]
		struct MyVertex
		{
			public Vector3 pos;
		}

		Device m_device;
		Texture2D m_renderTarget;
		RenderTargetView m_renderTargetView;
		IntSize m_renderTargetSize;
		RasterizerState m_rasterizerState;

		InputLayout m_layout;
		SlimDX.Direct3D11.Buffer m_vertexBuffer;

		Effect m_effect;
		EffectTechnique m_technique;
		EffectPass m_pass;

		EffectScalarVariable m_simpleTint;
		EffectScalarVariable m_tileSizeVariable;
		EffectVectorVariable m_colrowVariable;
		EffectVectorVariable m_renderOffsetVariable;

		SlimDX.Direct3D11.Buffer m_tileBuffer;
		ShaderResourceView m_tileBufferView;

		ShaderResourceView m_tileTextureView;
		ShaderResourceView m_colorBufferView;

		public SingleQuad11(Device device, SlimDX.Direct3D11.Buffer colorBuffer)
		{
			m_device = device;

			var context = m_device.ImmediateContext;

			/* Create shader */
			m_effect = LoadEffect(device);

			m_technique = m_effect.GetTechniqueByName("full");
			m_pass = m_technique.GetPassByIndex(0);
			m_layout = new InputLayout(m_device, m_pass.Description.Signature, new[] {
				new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
			});

			m_tileSizeVariable = m_effect.GetVariableByName("g_tileSize").AsScalar();
			m_colrowVariable = m_effect.GetVariableByName("g_colrow").AsVector();
			m_renderOffsetVariable = m_effect.GetVariableByName("g_renderOffset").AsVector();
			m_simpleTint = m_effect.GetVariableByName("g_simpleTint").AsScalar();


			/* color buffer view */
			m_colorBufferView = new ShaderResourceView(device, colorBuffer, new ShaderResourceViewDescription()
			{
				Format = SlimDX.DXGI.Format.R32_UInt,
				Dimension = ShaderResourceViewDimension.Buffer,
				ElementWidth = colorBuffer.Description.SizeInBytes / sizeof(uint),
				ElementOffset = 0,
			});

			m_effect.GetVariableByName("g_colorBuffer").AsResource().SetResource(m_colorBufferView);


			/* Create vertices */

			var vertexSize = Marshal.SizeOf(typeof(MyVertex));

			using (var stream = new DataStream(4 * vertexSize, true, true))
			{
				stream.Write(new MyVertex() { pos = new Vector3(0.0f, 0.0f, 0.0f), });
				stream.Write(new MyVertex() { pos = new Vector3(0.0f, 1.0f, 0.0f), });
				stream.Write(new MyVertex() { pos = new Vector3(1.0f, 0.0f, 0.0f), });
				stream.Write(new MyVertex() { pos = new Vector3(1.0f, 1.0f, 0.0f), });

				stream.Position = 0;

				m_vertexBuffer = new SlimDX.Direct3D11.Buffer(m_device, stream, new BufferDescription()
				{
					BindFlags = BindFlags.VertexBuffer,
					CpuAccessFlags = CpuAccessFlags.None,
					OptionFlags = ResourceOptionFlags.None,
					SizeInBytes = 4 * vertexSize,
					Usage = ResourceUsage.Immutable,
				});
			}


			//create world matrix
			Matrix w = Matrix.Identity;
			w *= Matrix.Scaling(2.0f, 2.0f, 0);
			w *= Matrix.Translation(-1.0f, -1.0f, 0);
			m_effect.GetVariableByName("g_world").AsMatrix().SetMatrix(w);
		}

		static Effect LoadEffect(Device device)
		{
			Effect effect;

			var ass = System.Reflection.Assembly.GetCallingAssembly();

			var stream = ass.GetManifestResourceStream("Dwarrowdelf.Client.TileControl.SingleQuad11.fx");

			using (var reader = new System.IO.StreamReader(stream))
			{
				var str = reader.ReadToEnd();

				using (var byteCode = ShaderBytecode.Compile(str, "fx_5_0", ShaderFlags.EnableStrictness, EffectFlags.None))
					effect = new Effect(device, byteCode);
			}

			return effect;
		}

		public void SetRenderTarget(Texture2D renderTexture)
		{
			SetupRenderTarget(renderTexture);

			SetupTileBuffer();
		}

		void SetupRenderTarget(Texture2D renderTexture)
		{
			if (m_renderTargetView != null)
			{
				m_renderTargetView.Dispose();
				m_renderTargetView = null;
			}

			if (m_rasterizerState != null)
			{
				m_rasterizerState.Dispose();
				m_rasterizerState = null;
			}

			var renderWidth = renderTexture.Description.Width;
			var renderHeight = renderTexture.Description.Height;
			var device = renderTexture.Device;
			var context = device.ImmediateContext;

			/* Setup render target */

			m_renderTarget = renderTexture;
			m_renderTargetSize = new IntSize(renderWidth, renderHeight);

			m_renderTargetView = new RenderTargetView(device, renderTexture);

			m_rasterizerState = RasterizerState.FromDescription(device, new RasterizerStateDescription()
			{
				FillMode = FillMode.Solid,
				CullMode = CullMode.None,
				IsDepthClipEnabled = false,
			});

			context.OutputMerger.SetTargets(m_renderTargetView);
			context.Rasterizer.SetViewports(new Viewport(0, 0, renderWidth, renderHeight, 0.0f, 1.0f));
			context.Rasterizer.State = m_rasterizerState;
		}

		void SetupTileBuffer()
		{
			if (m_tileBuffer != null)
			{
				m_tileBuffer.Dispose();
				m_tileBuffer = null;
			}

			if (m_tileBufferView != null)
			{
				m_tileBufferView.Dispose();
				m_tileBufferView = null;
			}

			const int minTileSize = 2;
			var tileBufferWidth = (int)Math.Ceiling((double)m_renderTargetSize.Width / minTileSize + 1) | 1;
			var tileBufferHeight = (int)Math.Ceiling((double)m_renderTargetSize.Height / minTileSize + 1) | 1;

			m_tileBuffer = new SlimDX.Direct3D11.Buffer(m_device, new BufferDescription()
			{
				BindFlags = BindFlags.ShaderResource,
				CpuAccessFlags = CpuAccessFlags.Write,
				OptionFlags = ResourceOptionFlags.StructuredBuffer,
				SizeInBytes = tileBufferWidth * tileBufferHeight * Marshal.SizeOf(typeof(RenderTileDetailed)),
				StructureByteStride = Marshal.SizeOf(typeof(RenderTileDetailed)),
				Usage = ResourceUsage.Dynamic,
			});

			m_tileBufferView = new ShaderResourceView(m_device, m_tileBuffer, new ShaderResourceViewDescription()
			{
				Format = SlimDX.DXGI.Format.Unknown,
				Dimension = ShaderResourceViewDimension.Buffer,
				ElementWidth = tileBufferWidth * tileBufferHeight,
				ElementOffset = 0,
			});

			m_effect.GetVariableByName("g_tileBuffer").AsResource().SetResource(m_tileBufferView);
		}

		public void SetTileTextures(Texture2D textureArray)
		{
			if (m_tileTextureView != null)
			{
				m_tileTextureView.Dispose();
				m_tileTextureView = null;
			}

			m_tileTextureView = new ShaderResourceView(m_device, textureArray, new ShaderResourceViewDescription()
			{
				Format = textureArray.Description.Format,
				Dimension = ShaderResourceViewDimension.Texture2DArray,
				MipLevels = textureArray.Description.MipLevels,
				MostDetailedMip = 0,
				ArraySize = textureArray.Description.ArraySize,
			});

			m_effect.GetVariableByName("g_tileTextures").AsResource().SetResource(m_tileTextureView);
		}

		public void SendMapData(RenderData<RenderTileDetailed> mapData, int columns, int rows)
		{
			m_colrowVariable.Set(new Vector2(columns, rows));

			var arr = mapData.Grid;
			var arrLen = arr.Length;
			var elemSize = Marshal.SizeOf(arr.GetType().GetElementType());

#if asd
				unsafe
				{
					fixed (RenderTileDetailedD3D* p = mapData.ArrayGrid.Grid)
					{
						using (var stream = new DataStream((IntPtr)p, arrLen * elemSize, true, false))
						{
							var dataBox = new DataBox(elemSize * arrLen, elemSize * arrLen, stream);
							var region = new ResourceRegion(0, 0, 0, arrLen * elemSize, 1, 1);
							m_device.ImmediateContext.UpdateSubresource(dataBox, m_tileBuffer, 0, region);
						}
					}
				}
#else
			var box = m_device.ImmediateContext.MapSubresource(m_tileBuffer, MapMode.WriteDiscard, SlimDX.Direct3D11.MapFlags.None);
			var stream = box.Data;

			unsafe
			{
				fixed (RenderTileDetailed* p = mapData.Grid)
				{
					stream.WriteRange((IntPtr)p, arrLen * elemSize);
				}
			}

			m_device.ImmediateContext.UnmapSubresource(m_tileBuffer, 0);
#endif
		}

		public void Render(float tileSize, System.Windows.Point renderOffset)
		{
			if (m_renderTargetView == null || m_tileTextureView == null)
				return;

			m_tileSizeVariable.Set(tileSize);
			m_renderOffsetVariable.Set(new Vector2((float)renderOffset.X, (float)renderOffset.Y));
			m_simpleTint.Set(true);

			var vertexSize = Marshal.SizeOf(typeof(MyVertex));

			var context = m_device.ImmediateContext;
			context.InputAssembler.InputLayout = m_layout;
			context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
			context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(m_vertexBuffer, vertexSize, 0));

			for (int i = 0; i < m_technique.Description.PassCount; ++i)
			{
				m_pass.Apply(context);
				context.Draw(4, 0);
			}
		}

		#region IDisposable
		bool m_disposed;

		~SingleQuad11()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!m_disposed)
			{
				if (disposing)
				{
					// Dispose managed resources.
				}

				// Dispose unmanaged resources

				if (m_renderTargetView != null) { m_renderTargetView.Dispose(); m_renderTargetView = null; }
				if (m_layout != null) { m_layout.Dispose(); m_layout = null; }
				if (m_vertexBuffer != null) { m_vertexBuffer.Dispose(); m_vertexBuffer = null; }
				if (m_effect != null) { m_effect.Dispose(); m_effect = null; }
				if (m_tileBuffer != null) { m_tileBuffer.Dispose(); m_tileBuffer = null; }
				if (m_tileBufferView != null) { m_tileBufferView.Dispose(); m_tileBufferView = null; }
				if (m_tileTextureView != null) { m_tileTextureView.Dispose(); m_tileTextureView = null; }
				if (m_rasterizerState != null) { m_rasterizerState.Dispose(); m_rasterizerState = null; }
				if (m_colorBufferView != null) { m_colorBufferView.Dispose(); m_colorBufferView = null; }

				m_disposed = true;
			}
		}
		#endregion
	}
}
