using System;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

using UnityEngine.Rendering.Universal;

namespace ShadedTechnology.WindshieldRainAsset
{
    public class WindshieldBlurFeature : ScriptableRendererFeature
    {

#if UNITY_6000_0_OR_NEWER
        public class BlurData : ContextItem, IDisposable
        {
            // Textures used for the blit operations.
            RTHandle[] m_Texture;
            float[] m_TexturesSizeDivider;
            RTHandle m_TextureHelper;
            // Render graph texture handles.
            TextureHandle[] m_TextureHandle;
            TextureHandle m_TextureHandleHelper;

            // Scale bias is used to control how the blit operation is done. The x and y parameter controls the scale
            // and z and w controls the offset.
            static Vector4 scaleBias = new Vector4(1f, 1f, 0f, 0f);
            int m_index = 0;

            // The texture which contains the color buffer from the most resent blit operation.
            public TextureHandle texture;

            // Function used to initialize BlitDatat. Should be called before starting to use the class for each frame.
            public void Init(RenderGraph renderGraph, RenderTextureDescriptor targetDescriptor, string textureName, int iterations)
            {
                if (m_Texture == null || m_Texture.Length != (iterations + 1))
                {
                    if (m_Texture != null)
                    {
                        for (int i = 0; i < m_Texture.Length; ++i)
                        {
                            m_Texture[i]?.Release();
                        }
                    }
                    m_Texture = new RTHandle[iterations + 1];
                }
                if (m_TexturesSizeDivider == null || m_TexturesSizeDivider.Length != (iterations + 1))
                {
                    m_TexturesSizeDivider = new float[iterations + 1];
                    float last_divider = 1;
                    for (int i = 0; i < m_TexturesSizeDivider.Length; ++i)
                    {
                        m_TexturesSizeDivider[i] = last_divider;
                        last_divider *= 2;
                    }
                }
                if (m_TextureHandle == null || m_TextureHandle.Length != (iterations + 1))
                {
                    m_TextureHandle = new TextureHandle[iterations + 1];
                }

                // Checks if the texture name is valid and puts in default value if not.
                var texName = String.IsNullOrEmpty(textureName) ? "_BlurTextureData" : textureName;
                // Reallocate if the RTHandles are being initialized for the first time or if the targetDescriptor has changed since last frame.
                float lastValidDivider = 1;
                const int minSize = 10;
                RenderTextureDescriptor helperDescriptor = targetDescriptor;
                for (int i = 0; i < m_Texture.Length; ++i)
                {
                    var descriptorCopy = targetDescriptor;
                    int width = (int)(targetDescriptor.width / m_TexturesSizeDivider[i]);
                    int height = (int)(targetDescriptor.height / m_TexturesSizeDivider[i]);
                    if (width < minSize || height < minSize)
                    {
                        width = (int)(targetDescriptor.width / lastValidDivider);
                        height = (int)(targetDescriptor.height / lastValidDivider);
                    }
                    else
                    {
                        lastValidDivider = m_TexturesSizeDivider[i];
                    }
                    descriptorCopy.width = width;
                    descriptorCopy.height = height;
                    if (i == 1)
                    {
                        helperDescriptor = descriptorCopy;
                    }
                    RenderingUtils.ReAllocateHandleIfNeeded(ref m_Texture[i], descriptorCopy, FilterMode.Bilinear, TextureWrapMode.Clamp, name: texName + i);
                }
                RenderingUtils.ReAllocateHandleIfNeeded(ref m_TextureHelper, helperDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: texName + "Helper");
                // Create the texture handles inside render graph by importing the RTHandles in render graph.
                for (int i = 0; i < m_Texture.Length; ++i)
                {
                    m_TextureHandle[i] = renderGraph.ImportTexture(m_Texture[i]);
                }
                m_TextureHandleHelper = renderGraph.ImportTexture(m_TextureHelper);
                // Sets the active texture to the front buffer
                texture = m_TextureHandle[0];
            }

            // We will need to reset the texture handle after each frame to avoid leaking invalid texture handles
            // since the texture handles only lives for one frame.
            public override void Reset()
            {
                // Resets the color buffers to avoid carrying invalid references to the next frame.
                // This could be BlitData texture handles from last frame which will now be invalid.
                for (int i = 0; i < m_TextureHandle.Length; ++i)
                {
                    m_TextureHandle[i] = TextureHandle.nullHandle;
                }
                m_TextureHandleHelper = TextureHandle.nullHandle;
                texture = TextureHandle.nullHandle;
                // Reset the acrive texture to be the front buffer.
                m_index = 0;
            }

            // The data we use to transfer data to the render function.
            class PassData
            {
                // When makeing a blit operation we will need a source, a destination and a material.
                // The source and destination is used to know where to copy from and to.
                public TextureHandle source;
                public TextureHandle destination;
                // The material is used to transform the color buffer while copying.
                public Material material;
                public int pass;
                public Vector4 offsetVector;
                public float testIndex;
            }

            // For this function we don't take a material as argument to show that we should remember to reset values
            // we don't use to avoid leaking values from last frame.
            public void RecordBlitColor(RenderGraph renderGraph, ContextContainer frameData, int iterations, string outputGlobal = "")
            {
                // Check if BlitData's texture is valid if it isn't initialize BlitData.
                if (!texture.IsValid())
                {
                    // Setup the descriptor we use for BlitData. We should use the camera target's descriptor as a start.
                    var cameraData = frameData.Get<UniversalCameraData>();
                    var descriptor = cameraData.cameraTargetDescriptor;
                    // We disable MSAA for the blit operations.
                    descriptor.msaaSamples = 1;
                    // We disable the depth buffer, since we are only makeing transformations to the color buffer.
                    descriptor.depthBufferBits = 0;
                    Init(renderGraph, descriptor, null, iterations);
                }

                // Starts the recording of the render graph pass given the name of the pass
                // and outputting the data used to pass data to the execution of the render function.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("BlurColorPass", out var passData))
                {
                    // Fetch UniversalResourceData from frameData to retrive the camera's active color attachment.
                    var resourceData = frameData.Get<UniversalResourceData>();

                    // Remember to reset material since it contains the value from last frame.
                    // If we don't do this we would get the material last commited to the BlitPassData using RenderGraph
                    // since we reuse the object allocation.
                    passData.material = null;
                    passData.source = resourceData.activeColorTexture;
                    passData.destination = texture;

                    // Sets input attachment to the cameras color buffer.
                    builder.UseTexture(passData.source);
                    // Sets output attachment 0 to BlitData's active texture.
                    builder.SetRenderAttachment(passData.destination, 0);
                    if (!String.IsNullOrEmpty(outputGlobal))
                    {
                        builder.SetGlobalTextureAfterPass(passData.destination, Shader.PropertyToID(outputGlobal));
                    }

                    // Sets the render function.
                    builder.SetRenderFunc((PassData passData, RasterGraphContext rgContext) => ExecutePass(passData, rgContext));
                }
            }

            public void RecordFullScreenPass(RenderGraph renderGraph, string passName, Material material, int shaderPass, bool toHelper, float blurStrength, string outputGlobal = "")
            {
                // Checks if the data is previously initialized and if the material is valid.
                if (!texture.IsValid() || material == null)
                {
                    Debug.LogWarning("Invalid input texture handle, will skip fullscreen pass.");
                    return;
                }

                // Starts the recording of the render graph pass given the name of the pass
                // and outputting the data used to pass data to the execution of the render function.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                {

                    // Setting data to be used when executing the render function.
                    passData.material = new Material(material);

                    // Swap the active texture.
                    if (toHelper)
                    {
                        int index = m_index + 1;
                        //var targetDescriptor = new RenderTextureDescriptor(m_Texture[index].rt.width, m_Texture[index].rt.height, m_Texture[index].rt.graphicsFormat, m_Texture[index].rt.depthStencilFormat);
                        //RenderingUtils.ReAllocateHandleIfNeeded(ref m_TextureHelper, targetDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_BlitTextureDataHelper");
                        //m_TextureHandleHelper = renderGraph.ImportTexture(m_TextureHelper);

                        passData.testIndex = m_index * 2;
                        passData.offsetVector = new Vector4(blurStrength / m_Texture[m_index + 1].rt.width, 0, 0, 0);
                        passData.source = texture;
                        passData.destination = m_TextureHandleHelper;
                    }
                    else
                    {
                        passData.testIndex = m_index * 2 + 1;
                        passData.offsetVector = new Vector4(0, blurStrength / m_Texture[m_index + 1].rt.height, 0, 0);
                        passData.source = m_TextureHandleHelper;
                        passData.destination = m_TextureHandle[++m_index];
                    }

                    // Sets input attachment to BlitData's old active texture.
                    builder.UseTexture(passData.source);
                    // Sets output attachment 0 to BitData's new active texture.
                    builder.SetRenderAttachment(passData.destination, 0);

                    // Update the texture after switching.
                    if (!toHelper)
                    {
                        texture = passData.destination;
                    }
                    passData.pass = shaderPass;

                    if (!String.IsNullOrEmpty(outputGlobal))
                    {
                        builder.SetGlobalTextureAfterPass(passData.destination, Shader.PropertyToID(outputGlobal));
                    }

                    // Sets the render function.
                    builder.SetRenderFunc((PassData passData, RasterGraphContext rgContext) => ExecutePass(passData, rgContext));
                }
            }

            static void ExecutePass(PassData data, RasterGraphContext rgContext)
            {
                if (((RTHandle)data.source).rt == null)
                {
                    return;
                }

                if (data.material == null)
                {
                    Blitter.BlitTexture(rgContext.cmd, data.source, scaleBias, 0, false);
                }
                else
                {
                    data.material.SetVector("_WindshieldRain_Blur_Offsets", data.offsetVector);
                    Blitter.BlitTexture(rgContext.cmd, data.source, scaleBias, data.material, data.pass);
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < m_Texture.Length; ++i)
                {
                    m_Texture[i]?.Release();
                }
                m_TextureHelper?.Release();
            }
        }
#endif

        class BlurRenderPass : ScriptableRenderPass
        {
            private string m_grabTextureName;
            private float m_blurStrength;
            private Material m_material;
            private int m_iterations;

#if !UNITY_6000_0_OR_NEWER

#if UNITY_2022_1_OR_NEWER
            float[] m_TexturesSizeDivider;
            RTHandle[] m_TempRT_H;
            RTHandle[] m_TempRT_V;
            RTHandle m_TempGrab;
            const int minSize = 10;
#else
            int[] m_TempRT_H;
            int[] m_TempRT_V;
            int m_TempGrab;
            RenderTargetIdentifier m_Source;
#endif

            static readonly int k_Offsets = Shader.PropertyToID("_WindshieldRain_Blur_Offsets");
#endif

            public BlurRenderPass(string grabTextureName, float blurStrength, int iterations)
            {
                this.m_grabTextureName = grabTextureName;
                m_blurStrength = blurStrength;
                m_iterations = Mathf.Clamp(iterations, 1, 7);
#if UNITY_6000_0_OR_NEWER
                m_material = new Material(Shader.Find("Hidden/SeparableGlassBlurURP"));
#else

#if UNITY_2022_1_OR_NEWER
                m_material = new Material(Shader.Find("Hidden/SeparableGlassBlurURP"));
                m_TempRT_H = new RTHandle[m_iterations];
                m_TempRT_V = new RTHandle[m_iterations];
#else
                m_material = new Material(Shader.Find("Hidden/SeparableGlassBlurURP_Old"));
                m_TempRT_H = new int[m_iterations];
                m_TempRT_V = new int[m_iterations];
                for (int i = 0; i < m_iterations; ++i)
                {
                    m_TempRT_H[i] = Shader.PropertyToID("_TempBlurRT_H_" + i);
                    m_TempRT_V[i] = Shader.PropertyToID("_TempBlurRT_V_" + i);
                }
                m_TempGrab = Shader.PropertyToID("_TempGrab");
#endif
#endif
            }

#if UNITY_6000_0_OR_NEWER
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_material == null)
                {
                    m_material = new Material(Shader.Find("Hidden/SeparableGlassBlurURP"));
                }
                var blitTextureData = frameData.Create<BlurData>();
                if (blitTextureData == null)
                {
                    return;
                }
                blitTextureData.RecordBlitColor(renderGraph, frameData, m_iterations, m_grabTextureName);
                for (int i = 0; i < m_iterations; ++i)
                {
                    blitTextureData.RecordFullScreenPass(renderGraph, $"Blit horizontal {i}", m_material, 0, true, m_blurStrength);
                    blitTextureData.RecordFullScreenPass(renderGraph, $"Blit vertical {i}", m_material, 0, false, m_blurStrength, $"_GrabBlurTexture_{i}");
                }
            }
#else

#if !UNITY_2022_1_OR_NEWER
            public void Setup(in RenderTargetIdentifier source)
            {
                m_Source = source;
            }
#else
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (m_TexturesSizeDivider == null || m_TexturesSizeDivider.Length != (m_iterations + 1))
                {
                    m_TexturesSizeDivider = new float[m_iterations + 1];
                    float last_divider = 2;
                    for (int i = 0; i < m_TexturesSizeDivider.Length; ++i)
                    {
                        m_TexturesSizeDivider[i] = last_divider;
                        last_divider *= 2;
                    }
                }
                var descriptor = renderingData.cameraData.renderer.cameraColorTargetHandle.rt.descriptor;
                float lastValidDivider = 2;
                for (int i = 0; i < m_iterations; ++i)
                {
                    var descriptorCopy = descriptor;
                    int width = (int)(descriptor.width / m_TexturesSizeDivider[i]);
                    int height = (int)(descriptor.height / m_TexturesSizeDivider[i]);
                    if (width < minSize || height < minSize)
                    {
                        width = (int)(descriptor.width / lastValidDivider);
                        height = (int)(descriptor.height / lastValidDivider);
                    }
                    else
                    {
                        lastValidDivider = m_TexturesSizeDivider[i];
                    }
                    descriptorCopy.width = width;
                    descriptorCopy.height = height;
                    RenderingUtils.ReAllocateIfNeeded(ref m_TempRT_H[i], descriptorCopy, name: "_TempBlurRT_H_" + i);
                    RenderingUtils.ReAllocateIfNeeded(ref m_TempRT_V[i], descriptorCopy, name: "_TempBlurRT_V_" + i);
                }
                RenderingUtils.ReAllocateIfNeeded(ref m_TempGrab, descriptor, name: "_TempGrab");
            }
#endif

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_material == null)
                {
                    return;
                }

#if !UNITY_2022_1_OR_NEWER
                // If Setup wasn't called, try a safe fallback
                if (m_Source == BuiltinRenderTextureType.None)
                {
                    // fallback to camera target - might be necessary on some URP versions
                    m_Source = BuiltinRenderTextureType.CameraTarget;
                }
#endif

                var cmd = CommandBufferPool.Get("Windshield Blur Pass");
                using (new ProfilingScope(cmd, new ProfilingSampler("Windshield Blur")))
                {
#if !UNITY_2022_1_OR_NEWER
                    // Use camera descriptor to preserve format (HDR, sRGB) and disable depth & MSAA.
                    var cameraDesc = renderingData.cameraData.cameraTargetDescriptor;
                    cameraDesc.depthBufferBits = 0;
                    cameraDesc.msaaSamples = 1;

                    RenderTargetIdentifier currentSource = m_Source;
                    int fullW = cameraDesc.width;
                    int fullH = cameraDesc.height;
                    // final grabbed blurred texture
                    cmd.GetTemporaryRT(m_TempGrab, cameraDesc, FilterMode.Bilinear);
                    Blit(cmd, currentSource, m_TempGrab);
                    cmd.SetGlobalTexture(Shader.PropertyToID(m_grabTextureName), m_TempGrab);
                    for (int i = 0; i < m_iterations; ++i)
                    {
                        // create smaller RTs (divide by 2^i), but keep minimum size
                        var rtDesc = cameraDesc;
                        rtDesc.width = Mathf.Max(10, fullW >> (i + 1));
                        rtDesc.height = Mathf.Max(10, fullH >> (i + 1));

                        // allocate temporaries with the camera descriptor (keeps correct format)
                        cmd.GetTemporaryRT(m_TempRT_H[i], rtDesc, FilterMode.Bilinear);
                        cmd.GetTemporaryRT(m_TempRT_V[i], rtDesc, FilterMode.Bilinear);

                        // Horizontal pass
                        cmd.SetGlobalVector(k_Offsets, new Vector4(m_blurStrength / rtDesc.width, 0f, 0f, 0f));
                        Blit(cmd, currentSource, m_TempRT_H[i], m_material, 0);

                        // Vertical pass
                        cmd.SetGlobalVector(k_Offsets, new Vector4(0f, m_blurStrength / rtDesc.height, 0f, 0f));
                        Blit(cmd, m_TempRT_H[i], m_TempRT_V[i], m_material, 0);

                        // expose each iteration as a global texture: _GrabBlurTexture_{i}
                        cmd.SetGlobalTexture(Shader.PropertyToID($"_GrabBlurTexture_{i}"), new RenderTargetIdentifier(m_TempRT_V[i]));

                        currentSource = m_TempRT_V[i];
                    }

                    // clean up temporaries
                    for (int i = 0; i < m_iterations; ++i)
                    {
                        cmd.ReleaseTemporaryRT(m_TempRT_H[i]);
                        cmd.ReleaseTemporaryRT(m_TempRT_V[i]);
                    }
#else
                    RTHandle currentSource = renderingData.cameraData.renderer.cameraColorTargetHandle;
                    Blitter.BlitCameraTexture(cmd, currentSource, m_TempGrab);
                    cmd.SetGlobalTexture(Shader.PropertyToID(m_grabTextureName), m_TempGrab);
                    for (int i = 0; i < m_iterations; ++i)
                    {
                        // Horizontal pass
                        cmd.SetGlobalVector(k_Offsets, new Vector4(m_blurStrength / m_TempRT_H[i].rt.width, 0f, 0f, 0f));
                        Blitter.BlitCameraTexture(cmd, currentSource, m_TempRT_H[i], m_material, 0);

                        // Vertical pass
                        cmd.SetGlobalVector(k_Offsets, new Vector4(0f, m_blurStrength / m_TempRT_V[i].rt.height, 0f, 0f));
                        Blitter.BlitCameraTexture(cmd, m_TempRT_H[i], m_TempRT_V[i], m_material, 0);

                        // expose each iteration as a global texture: _GrabBlurTexture_{i}
                        cmd.SetGlobalTexture(Shader.PropertyToID($"_GrabBlurTexture_{i}"), m_TempRT_V[i]);

                        currentSource = m_TempRT_V[i];
                    }
#endif
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#endif

            public void Dispose(bool isPlaying)
            {
#if !UNITY_6000_0_OR_NEWER && UNITY_2022_1_OR_NEWER
                if (m_TempRT_H != null)
                {
                    for (int i = 0; i < m_TempRT_H.Length; ++i)
                    {
                        m_TempRT_H[i]?.Release();
                        m_TempRT_H[i] = null;
                    }
                }
                if (m_TempRT_V != null)
                {
                    for (int i = 0; i < m_TempRT_V.Length; ++i)
                    {
                        m_TempRT_V[i]?.Release();
                        m_TempRT_V[i] = null;
                    }
                }
#endif

                if (isPlaying)
                {
                    Destroy(m_material);
                }
                else
                {
                    DestroyImmediate(m_material);
                }
            }

        }

        [SerializeField] public int iterations = 3;
        [SerializeField] private float blurStrength = 2;
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        private BlurRenderPass blurRenderPass;

        public override void Create()
        {
            blurRenderPass = new BlurRenderPass("_WindshieldGrabTexture", blurStrength, iterations);
            blurRenderPass.renderPassEvent = renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (blurRenderPass == null)
            {
                return;
            }
#if !UNITY_6000_0_OR_NEWER && !UNITY_2022_1_OR_NEWER
            blurRenderPass.Setup(renderer.cameraColorTarget);
#endif
            renderer.EnqueuePass(blurRenderPass);
        }

        protected override void Dispose(bool disposing)
        {
            blurRenderPass.Dispose(Application.isPlaying);
        }
    }
}