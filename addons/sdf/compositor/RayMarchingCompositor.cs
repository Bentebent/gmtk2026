using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Godot;
using Godot.Collections;
using Array = System.Array;

namespace GMTK2026.addons.sdf.compositor;

[Tool]
[GlobalClass]
public partial class RayMarchingCompositor : CompositorEffect {
    private Rid _computePipeline;
    private Rid _perFrameBuffer;
    private RenderingDevice _renderingDevice;
    private Rid _sdfShapeBuffer;
    private Rid _shader;
    private Rid _transformBuffer;

    public RayMarchingCompositor() {
        EffectCallbackType = EffectCallbackTypeEnum.PostOpaque;

        RenderingServer.CallOnRenderThread(Callable.From(Initialize));
    }

    public override void _Notification(int what) {
        if (what != NotificationPredelete) {
            return;
        }

        if (_perFrameBuffer.IsValid) {
            _renderingDevice.FreeRid(_perFrameBuffer);
        }

        if (_sdfShapeBuffer.IsValid) {
            _renderingDevice.FreeRid(_sdfShapeBuffer);
        }

        if (_transformBuffer.IsValid) {
            _renderingDevice.FreeRid(_transformBuffer);
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

        var spirv = GD.Load<RDShaderFile>("res://addons/sdf/shaders/ray_marching.glsl").GetSpirV();
        _shader = _renderingDevice.ShaderCreateFromSpirV(spirv);

        if (!_shader.IsValid) {
            GD.PrintErr("Failed to create shader!");
            return;
        }

        _computePipeline = _renderingDevice.ComputePipelineCreate(_shader);
        GD.Print("Compute pipeline created!");

        _perFrameBuffer = InitializeBuffer(sizeof(float) * 33);
        _transformBuffer = InitializeBuffer(sizeof(float) * 1000);
        _sdfShapeBuffer = InitializeBuffer(sizeof(float) * 1000);
        /*
        var placeholderData = new List<float>();
        placeholderData.AddRange(Enumerable.Repeat(0.0f, 32));
        ReadOnlySpan<byte> byteSpan = MemoryMarshal.AsBytes(placeholderData.ToArray().AsSpan());
        _perFrameBuffer = _renderingDevice.StorageBufferCreate((uint)byteSpan.Length, byteSpan.ToArray());
        GD.Print("Storage buffer created!");
        */
    }

    private Rid InitializeBuffer(int initialSize) {
        var placeholderData = new List<byte>();
        placeholderData.AddRange(Enumerable.Repeat((byte)0, initialSize));
        return _renderingDevice.StorageBufferCreate((uint)placeholderData.Count, placeholderData.ToArray());
    }

    private void UpdateInstanceBuffers() {
        List<byte> shapeBuffer = [];
        List<byte> shapeData = [];
        List<byte> instanceData = [];

        foreach (var (_, instances) in SdfRegistry.Instance.SdfInstances) {
            var count = 0;
            foreach (var instance in instances) {
                if (!instance.IsVisible()) {
                    continue;
                }

                shapeData.AddRange(instance.Resource.GetBytes());
                instanceData.AddRange(instance.GetInstanceData());
                count++;
            }

            shapeBuffer.AddRange(BitConverter.GetBytes(count));
        }

        shapeBuffer.AddRange(shapeData);
        _renderingDevice.BufferUpdate(_sdfShapeBuffer, 0, (uint)shapeBuffer.Count, shapeBuffer.ToArray());
        _renderingDevice.BufferUpdate(_transformBuffer, 0, (uint)instanceData.Count, instanceData.ToArray());
    }

    public override void _RenderCallback(int effectCallbackType, RenderData renderData) {
        if (_renderingDevice == null || !_computePipeline.IsValid || SdfRegistry.Instance == null) {
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
        var zGroups = 1;

        UpdateInstanceBuffers();

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
            clipToWorld.W
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

            var transformBuffer = new RDUniform();
            transformBuffer.UniformType = RenderingDevice.UniformType.StorageBuffer;
            transformBuffer.Binding = 1;
            transformBuffer.AddId(_transformBuffer);

            var sdfBuffer = new RDUniform();
            sdfBuffer.UniformType = RenderingDevice.UniformType.StorageBuffer;
            sdfBuffer.Binding = 2;
            sdfBuffer.AddId(_sdfShapeBuffer);

            var bufferSet = UniformSetCacheRD.GetCache(_shader, 0,
                new Array<RDUniform> { perFrameBuffer, transformBuffer, sdfBuffer });

            var colorLayerUniform = new RDUniform();
            colorLayerUniform.UniformType = RenderingDevice.UniformType.Image;
            colorLayerUniform.Binding = 0;
            colorLayerUniform.AddId(colorLayer);

            var textureSet = UniformSetCacheRD.GetCache(_shader, 1, new Array<RDUniform> { colorLayerUniform });

            var computeList = _renderingDevice.ComputeListBegin();
            _renderingDevice.ComputeListBindComputePipeline(computeList, _computePipeline);
            _renderingDevice.ComputeListBindUniformSet(computeList, bufferSet, 0);
            _renderingDevice.ComputeListBindUniformSet(computeList, textureSet, 1);
            _renderingDevice.ComputeListSetPushConstant(computeList, pushConstantBytes.ToArray(),
                (uint)pushConstantBytes.Count);
            _renderingDevice.ComputeListDispatch(computeList, (uint)xGroups, (uint)yGroups, (uint)zGroups);
            _renderingDevice.ComputeListEnd();
        }
    }
}