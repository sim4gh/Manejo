using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{
    [CreateAssetMenu(fileName = "RainPostProcessProfile", menuName = "WindshieldRain/RainPostProcessProfile")]
    public class RainPostProcessProfile : ScriptableObject
    {
        [SerializeField]
        public RainPostProcess[] postProcesses;

        void InitRenderTexture(Vector2Int resolution, ref RenderTexture renderTexture)
        {
            renderTexture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGBFloat);
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
            renderTexture.filterMode = FilterMode.Point;
        }

        public void InitPostProcesses(Vector2Int resolution)
        {
            foreach (RainPostProcess postProcess in postProcesses)
            {
                InitRenderTexture(resolution, ref postProcess.renderTexture);
                if (postProcess.material == null)
                {
                    continue;
                }
                postProcess.material.SetVector("_Resolution", new Vector4(resolution.x, resolution.y));
                foreach (MaterialTexture materialTexture in postProcess.texturesToSet)
                {
                    materialTexture.material.SetTexture(materialTexture.textureName, postProcess.renderTexture);
                }
            }
        }
        public Texture UpdatePostProcesses(Texture currentTexture)
        {
            for (int i = 0; i < postProcesses.Length; ++i)
            {
                if (postProcesses[i].material == null)
                {
                    foreach (MaterialTexture materialTexture in postProcesses[i].texturesToSet)
                    {
                        materialTexture.material.SetTexture(materialTexture.textureName, currentTexture);
                    }
                    continue;
                }
                if (i == 0)
                {
                    postProcesses[i].material.SetTexture("_MainTex", currentTexture);
                    Graphics.Blit(currentTexture, postProcesses[i].renderTexture, postProcesses[i].material);
                }
                else
                {
                    postProcesses[i].material.SetTexture("_MainTex", postProcesses[i - 1].renderTexture);
                    Graphics.Blit(postProcesses[i - 1].renderTexture, postProcesses[i].renderTexture, postProcesses[i].material);
                }
            }

            if (postProcesses.Length == 0) {
                return currentTexture;
            }
            return postProcesses[postProcesses.Length - 1].renderTexture;
        }
    }
}
