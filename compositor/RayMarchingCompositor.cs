using System;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace GMTK2026.compositor {
	[Tool]
	[GlobalClass]
	public partial class RayMarchingCompositor : CompositorEffect {
		private RenderingDevice _renderingDevice;
		private Rid _shader;
		private Rid _computePipeline;
		private Rid _perFrameBuffer;
		
		public RayMarchingCompositor() {
			EffectCallbackType = EffectCallbackTypeEnum.PostOpaque;
			
			RenderingServer.CallOnRenderThread(Callable.From(Initialize));
		}

		public override void _Notification(int what) {
			if (what != NotificationPredelete) {
				return;
			}

			if (_shader.IsValid) {
				_renderingDevice.FreeRid(_shader);
			}
		}

		private void Initialize() {
			_renderingDevice = RenderingServer.GetRenderingDevice();
			if (_renderingDevice == null) {
				GD.PrintErr("No rendering device found!");
				return;
			}

			var spirv = GD.Load<RDShaderFile>("res://shaders/ray_marching.glsl").GetSpirV();
			_shader = _renderingDevice.ShaderCreateFromSpirV(spirv);

			if (!_shader.IsValid) {
				GD.PrintErr("Failed to create shader!");
				return;
			}
			
			_computePipeline = _renderingDevice.ComputePipelineCreate(_shader);
			GD.Print("Compute pipeline created!");

			var placeholderData = new List<float>();
			placeholderData.AddRange(Enumerable.Repeat(0.0f, 32));
			ReadOnlySpan<byte> byteSpan = MemoryMarshal.AsBytes(placeholderData.ToArray().AsSpan());
			_perFrameBuffer = _renderingDevice.StorageBufferCreate((uint)byteSpan.Length, byteSpan.ToArray());
			GD.Print("Storage buffer created!");
	}

		public override void _RenderCallback(int effectCallbackType, RenderData renderData) {
			if (_renderingDevice == null || !_computePipeline.IsValid) {
				return;
			}

			var renderSceneBuffers = (RenderSceneBuffersRD)renderData.GetRenderSceneBuffers();
			if (renderSceneBuffers == null) {
				return;
			}
			
			var size = renderSceneBuffers.GetInternalSize();
			if (size.LengthSquared() == 0) {
				return;
			}

			// Ceil to closest int
			// Magic number (8) needs to match compute shader
			var xGroups = (size.X - 1) / 8 + 1;
			var yGroups = (size.Y - 1) / 8 + 1;
			int zGroups = 1;

			var worldToClip = renderData.GetRenderSceneData().GetCamProjection() *
			                  new Projection(renderData.GetRenderSceneData().GetCamTransform().AffineInverse());
			var clipToWorld = new Projection(renderData.GetRenderSceneData().GetCamTransform()) *
			                   renderData.GetRenderSceneData().GetCamProjection().Inverse();

			Vector4[] perFrameBufferData = [
				worldToClip.X,
				worldToClip.Y,
				worldToClip.Z,
				worldToClip.W,
				clipToWorld.X,
				clipToWorld.Y,
				clipToWorld.Z,
				clipToWorld.W,
			];
			ReadOnlySpan<byte> byteSpan = MemoryMarshal.AsBytes(perFrameBufferData.AsSpan());
			_renderingDevice.BufferUpdate(_perFrameBuffer, 0, (uint)byteSpan.Length, byteSpan.ToArray());
			
			float[] pushConstants = [size.X, size.Y];
			var pushConstantBytes = new List<byte>();
			Array.ForEach(pushConstants, v => pushConstantBytes.AddRange(BitConverter.GetBytes(v)));
			
			var viewCount = (int)renderSceneBuffers.GetViewCount();
			for (uint i = 0; i < viewCount; i++) {
				var colorLayer = renderSceneBuffers.GetColorLayer(i);
				
				var perFrameBuffer = new RDUniform();
				perFrameBuffer.UniformType = RenderingDevice.UniformType.StorageBuffer;
				perFrameBuffer.Binding = 0;
				perFrameBuffer.AddId(_perFrameBuffer);
				
				var bufferSet = UniformSetCacheRD.GetCache(_shader, 0, new Godot.Collections.Array<RDUniform>(){perFrameBuffer});
				
				var colorLayerUniform = new RDUniform();
				colorLayerUniform.UniformType = RenderingDevice.UniformType.Image;
				colorLayerUniform.Binding = 0;
				colorLayerUniform.AddId(colorLayer);
				
				var textureSet = UniformSetCacheRD.GetCache(_shader, 1, new Godot.Collections.Array<RDUniform>(){colorLayerUniform});
				
				var computeList = _renderingDevice.ComputeListBegin();
				_renderingDevice.ComputeListBindComputePipeline(computeList, _computePipeline);
				_renderingDevice.ComputeListBindUniformSet(computeList, bufferSet, 0);
				_renderingDevice.ComputeListBindUniformSet(computeList, textureSet, 1);
				_renderingDevice.ComputeListSetPushConstant(computeList, pushConstantBytes.ToArray(), (uint)pushConstantBytes.Count);
				_renderingDevice.ComputeListDispatch(computeList, (uint)xGroups, (uint)yGroups, (uint)zGroups);
				_renderingDevice.ComputeListEnd();
			}
		}
	}
}
