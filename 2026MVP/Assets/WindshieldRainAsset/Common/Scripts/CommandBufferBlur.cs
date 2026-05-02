using UnityEngine;
using UnityEngine.Rendering;

/*
Based on: https://github.com/andydbc/unity-frosted-glass

MIT License

Copyright (c) 2018 Andy Duboc

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace ShadedTechnology.WindshieldRainAsset
{

    [ExecuteInEditMode]
    [ImageEffectAllowedInSceneView]
    [RequireComponent(typeof(Camera))]
    public class CommandBufferBlur : MonoBehaviour
    {
        public int m_Iterations = 3;
        public float m_BlurStrength = 2.0f;

        Shader _Shader;

        Material _Material = null;

        Camera _Camera = null;
        CommandBuffer _CommandBuffer = null;
        const int _minResolution = 10;

        Vector2 _ScreenResolution = Vector2.zero;
        RenderTextureFormat _TextureFormat = RenderTextureFormat.ARGB32;

        public void Cleanup()
        {
            if (!Initialized)
                return;

            _Camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, _CommandBuffer);
            _CommandBuffer = null;
            Object.DestroyImmediate(_Material);
        }

        public void OnEnable()
        {
            Cleanup();
            Initialize();
        }

        public void OnDisable()
        {
            Cleanup();
        }

        public bool Initialized
        {
            get { return _CommandBuffer != null; }
        }

        void Initialize()
        {
            if (Initialized)
                return;

            if (!_Shader)
            {
                _Shader = Shader.Find("Hidden/SeparableGlassBlur");

                if (!_Shader)
                    throw new MissingReferenceException("Unable to find required shader \"Hidden/SeparableGlassBlur\"");
            }

            if (!_Material)
            {
                _Material = new Material(_Shader);
                _Material.hideFlags = HideFlags.HideAndDontSave;
            }

            _Camera = GetComponent<Camera>();

            if (_Camera.allowHDR && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR))
                _TextureFormat = RenderTextureFormat.DefaultHDR;

            _CommandBuffer = new CommandBuffer();
            _CommandBuffer.name = "Blur screen";

            Vector2[] sizes = new Vector2[m_Iterations];

            int last_divider = 2;
            for (int i = 0; i < m_Iterations; ++i)
            {
                sizes[i] = new Vector2(Screen.width / last_divider, Screen.height / last_divider);
                if ((Screen.width / (last_divider * 2)) > _minResolution && (Screen.height / (last_divider * 2)) > _minResolution)
                {
                    last_divider *= 2;
                }
            }

            for (int i = 0; i < m_Iterations; ++i)
            {
                int screenCopyID = Shader.PropertyToID("_ScreenCopyTexture");
                _CommandBuffer.GetTemporaryRT(screenCopyID, -1, -1, 0, FilterMode.Bilinear, _TextureFormat);
                _CommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, screenCopyID);

                int blurredID = Shader.PropertyToID("_Grab" + i + "_Temp1");
                int blurredID2 = Shader.PropertyToID("_Grab" + i + "_Temp2");
                _CommandBuffer.GetTemporaryRT(blurredID, (int)sizes[i].x, (int)sizes[i].y, 0, FilterMode.Bilinear, _TextureFormat);
                _CommandBuffer.GetTemporaryRT(blurredID2, (int)sizes[i].x, (int)sizes[i].y, 0, FilterMode.Bilinear, _TextureFormat);

                _CommandBuffer.Blit(screenCopyID, blurredID);
                _CommandBuffer.ReleaseTemporaryRT(screenCopyID);

                _CommandBuffer.SetGlobalVector("offsets", new Vector4(m_BlurStrength / sizes[i].x, 0, 0, 0));
                _CommandBuffer.Blit(blurredID, blurredID2, _Material);
                _CommandBuffer.SetGlobalVector("offsets", new Vector4(0, m_BlurStrength / sizes[i].y, 0, 0));
                _CommandBuffer.Blit(blurredID2, blurredID, _Material);

                _CommandBuffer.SetGlobalTexture("_GrabBlurTexture_" + i, blurredID);
            }

            _Camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, _CommandBuffer);

            _ScreenResolution = new Vector2(Screen.width, Screen.height);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Cleanup();
            Initialize();
        }
#endif

        void OnPreRender()
        {
            if (_ScreenResolution != new Vector2(Screen.width, Screen.height))
                Cleanup();

            Initialize();
        }
    }
}
