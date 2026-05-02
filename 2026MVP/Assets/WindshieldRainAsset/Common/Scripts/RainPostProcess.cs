using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{
    [System.Serializable]
    public class MaterialTexture
    {
        public string textureName;
        public Material material;
    }

    [System.Serializable]
    public class RainPostProcess
    {
        [SerializeField]
        public Material material;
        [SerializeField]
        [HideInInspector]
        public RenderTexture renderTexture;
        [SerializeField]
        public MaterialTexture[] texturesToSet;
    }
}
