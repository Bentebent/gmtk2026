using System;
using Godot;
using System.Collections.Generic;

namespace GMTK2026.compositor {
    [Tool]
    [GlobalClass]
    public partial class RayMarchingCompositor : CompositorEffect {
        private RenderingDevice _renderingDevice;
        private Rid _shader;
        private Rid _computePipeline;
        
        public RayMarchingCompositor() {
            EffectCallbackType = EffectCallbackTypeEnum.PostOpaque;
            
            RenderingServer.CallOnRenderThread(Callable.From(_Init));
        }

        public void _Init() {
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
        }

        public override void _Notification(int what) {
            if (what == NotificationPostinitialize) {
                if (_shader.IsValid) {
                    _renderingDevice.FreeRid(_shader);
                }
            }
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

            float[] pushConstants = [
                size.X,
                size.Y,
                0.0f,
                0.0f
            ];

            var pushConstantBytes = new List<byte>();
            Array.ForEach(pushConstants, x => { pushConstantBytes.AddRange(BitConverter.GetBytes(x)); });
            
            
            var viewCount = (int)renderSceneBuffers.GetViewCount();
            for (uint i = 0; i < viewCount; i++) {
                var inputImage = renderSceneBuffers.GetColorLayer(i);
                
                var uniform = new RDUniform();
                uniform.UniformType = RenderingDevice.UniformType.Image;
                uniform.Binding = 0;
                uniform.AddId(inputImage);
                
                var uniformSet = UniformSetCacheRD.GetCache(_shader, 0, new Godot.Collections.Array<RDUniform>(){uniform});

                var computeList = _renderingDevice.ComputeListBegin();
                _renderingDevice.ComputeListBindComputePipeline(computeList, _computePipeline);
                _renderingDevice.ComputeListBindUniformSet(computeList, uniformSet, 0);
                _renderingDevice.ComputeListSetPushConstant(computeList, pushConstantBytes.ToArray(), (uint)pushConstantBytes.Count);
                _renderingDevice.ComputeListDispatch(computeList, (uint)xGroups, (uint)yGroups, (uint)zGroups);
                _renderingDevice.ComputeListEnd();
            }
        }
    }
}
