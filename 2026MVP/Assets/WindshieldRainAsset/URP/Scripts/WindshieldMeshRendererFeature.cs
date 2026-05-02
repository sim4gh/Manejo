using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ShadedTechnology.WindshieldRainAsset
{

    public class WindshieldMeshRendererFeature : ScriptableRendererFeature
    {
        class WindshieldMeshRenderPass : ScriptableRenderPass
        {

            public WindshieldMeshRenderPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

            private class PassData
            {
                public List<WindshieldMeshRenderer> entries;
            }

#if UNITY_6000_0_OR_NEWER
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Draw Custom Meshes", out var passData))
                {
                    passData.entries = WindshieldMeshRenderer.ActiveRenderers;

                    UniversalResourceData resourceData = frameContext.Get<UniversalResourceData>();
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        foreach (var e in data.entries)
                        {
                            if (e.enabled && e.mesh && e.material)
                            {
                                ctx.cmd.DrawMesh(e.mesh, e.transform.localToWorldMatrix, e.material, 0, 0);
                            }
                        }
                    });
                }
            }
#else
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                const string profilerTag = "Draw Windshield Meshes";
                var cmd = CommandBufferPool.Get(profilerTag);

                using (new ProfilingScope(cmd, profilingSampler))
                {
                    var camera = renderingData.cameraData.camera;
                    var entries = WindshieldMeshRenderer.ActiveRenderers;

                    foreach (var e in entries)
                    {
                        if (e && e.enabled && e.mesh && e.material)
                        {
                            cmd.DrawMesh(e.mesh, e.transform.localToWorldMatrix, e.material, 0, 0);
                        }
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#endif

        }

        private WindshieldMeshRenderPass pass;

        public override void Create()
        {
            pass = new WindshieldMeshRenderPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(pass);
        }
    }
}
