using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{
    public struct Wiper
    {
        public Vector2 origin;
        public Vector4 pos;
        public Vector4 prevPos;
    }

    [System.Serializable]
    public class WiperObject
    {
        public Transform originPos;
        public Transform startPos;
        public Transform endPos;
        [HideInInspector]
        public Queue<Vector2> prevStartPositions = new Queue<Vector2>();
        public Queue<Vector2> prevEndPositions = new Queue<Vector2>();
        [HideInInspector]
        public Vector2 lastStartPos;
        [HideInInspector]
        public Vector2 lastEndPos;

        public void InitWiper(int delay, WindshieldPlane windshieldPlane)
        {
            Vector2 start_pos = windshieldPlane.WorldPosToWindshieldPos(startPos.position);
            Vector2 end_pos = windshieldPlane.WorldPosToWindshieldPos(endPos.position);
            lastStartPos = start_pos;
            lastEndPos = end_pos;
            for (int i = 0; i < delay; ++i)
            {
                prevStartPositions.Enqueue(start_pos);
                prevEndPositions.Enqueue(end_pos);
            }
        }

        public void UpdateWiper(WindshieldPlane windshieldPlane)
        {
            Vector2 start_pos = windshieldPlane.WorldPosToWindshieldPos(startPos.position);
            Vector2 end_pos = windshieldPlane.WorldPosToWindshieldPos(endPos.position);

            prevStartPositions.Enqueue(start_pos);
            prevEndPositions.Enqueue(end_pos);
            lastStartPos = prevStartPositions.Dequeue();
            lastEndPos = prevEndPositions.Dequeue();
        }
    }

    [System.Serializable]
    public class WipersMaterialTexture : MaterialTexture
    {
        public bool useCustomName = false;
    }

    [AddComponentMenu("Windshield Rain Asset/Wipers")]
    public class Wipers : MonoBehaviour
    {
        [Space]
        [Header("Shader Settings")]
        public int delay = 2;
        public WindshieldRain rainScript;
        [HideInInspector]
        public ComputeShader computeShader;

        [Space]
        [Range(0, 10)]
        public float wipingDisappearingRate = 1.0f;
        [Range(0, 10)]
        public float smudgeDisappearingRate = 1.0f;
        [Range(0, 1)]
        public float smudgesNoiseStrength = 0.5f;
        public float smudgesNoiseScale = 1;

        [Space]
        public WipersMaterialTexture[] texturesToSet = new WipersMaterialTexture[0];

        [Space]
        public WiperObject[] wipers;

        private Wiper[] wipersData;
        private int kernelIndex;
        private RenderTexture renderTexture1;
        private RenderTexture renderTexture2;
        private ComputeBuffer wipersBuffer;

        private Vector2Int Resolution
        {
            get
            {
                return rainScript.m_Resolution;
            }
        }

        bool textureSwap = false;
        RenderTexture getPrevTexture()
        {
            return (textureSwap ? renderTexture1 : renderTexture2);
        }
        public RenderTexture getCurrTexture()
        {
            return (textureSwap ? renderTexture2 : renderTexture1);
        }

        void InitRenderTexture(ref RenderTexture renderTexture)
        {
            renderTexture = new RenderTexture(Resolution.x, Resolution.y, 0, RenderTextureFormat.ARGBFloat);
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
            renderTexture.filterMode = FilterMode.Point;
        }
        void InitializeWipersBuffer()
        {
            if (wipers.Length == 0)
            {
                Debug.LogWarning("No Wipers Objects assigned in the Wipers script!");
                return;
            }

            wipersBuffer = new ComputeBuffer(wipers.Length, // number of elements in the buffer
                Marshal.SizeOf(typeof(Wiper)) // size of each element
            );
            wipersData = new Wiper[wipers.Length];
            UpdateWipersBuffer();
        }

        public void UpdateWipersBuffer()
        {
            if (wipersData == null)
            {
                return;
            }
            for (int i = 0; i < wipersData.Length; ++i)
            {
                wipersData[i] = GetWiperData(wipers[i]);
            }
            wipersBuffer.SetData(wipersData);
        }

        void ReleaseBuffers()
        {
            if (wipersBuffer != null)
            {
                wipersBuffer.Release();
            }
        }
        private void OnDestroy()
        {
            ReleaseBuffers();
        }
        void InitKernelIndicies()
        {
            kernelIndex = computeShader.FindKernel("CSMain");
        }

        private bool _isInitialized = false;

        public bool IsInitialized()
        {
            return _isInitialized;
        }

        // Start is called before the first frame update
        void Start()
        {
            computeShader = Instantiate(computeShader);
            InitKernelIndicies();
            InitRenderTexture(ref renderTexture1);
            InitRenderTexture(ref renderTexture2);

            ReleaseBuffers();

            InitializeWipersBuffer();
            InitWipers();
            computeShader.SetInt("_WipersCount", wipers.Length);
            computeShader.SetInts("_Resolution", new int[] { Resolution.x, Resolution.y });
            computeShader.SetFloats("_WindshieldPlaneSize", new float[] { rainScript.m_WindshieldPlane.width, rainScript.m_WindshieldPlane.height });
            _isInitialized = true;
        }

        public void UpdateWipers(float deltaTime)
        {
            for (int i = 0; i < wipers.Length; ++i)
            {
                wipers[i].UpdateWiper(rainScript.m_WindshieldPlane);
            }
            UpdateWipersBuffer();
            if (wipersData == null || wipersBuffer == null)
            {
                return;
            }

            computeShader.SetFloat("_DeltaTime", deltaTime);
            computeShader.SetFloat("_WipingDisappearingRate", wipingDisappearingRate);
            computeShader.SetFloat("_SmudgeDisappearingRate", smudgeDisappearingRate);
            computeShader.SetFloat("_SmudgesNoiseStrength", smudgesNoiseStrength);
            computeShader.SetFloat("_SmudgesNoiseScale", smudgesNoiseScale);

            int numGroupsRenderX = Mathf.CeilToInt(Resolution.x / (float)WindshieldRain.NUM_THREADS_PER_GROUP);
            int numGroupsRenderY = Mathf.CeilToInt(Resolution.y / (float)WindshieldRain.NUM_THREADS_PER_GROUP);
            computeShader.SetBuffer(kernelIndex, "_Wipers", wipersBuffer);
            computeShader.SetTexture(kernelIndex, "Result", getCurrTexture());
            computeShader.SetTexture(kernelIndex, "Prev", getPrevTexture());
            computeShader.SetTexture(kernelIndex, "DropsTexture", rainScript.GetCurrTexture());
            computeShader.Dispatch(kernelIndex, numGroupsRenderX, numGroupsRenderY, 1);

            foreach (MaterialTexture materialTexture in texturesToSet)
            {
                materialTexture.material.SetTexture(materialTexture.textureName, getCurrTexture());
            }

            textureSwap = !textureSwap;
        }

        void InitWipers()
        {
            for (int i = 0; i < wipers.Length; ++i)
            {
                Vector2 initPos = rainScript.m_WindshieldPlane.WorldPosToWindshieldPos(wipers[i].originPos.position);
                float initLenght = (rainScript.m_WindshieldPlane.WorldPosToWindshieldPos(wipers[i].endPos.position) - initPos).magnitude;
                wipers[i].InitWiper(delay, rainScript.m_WindshieldPlane);
            }
        }

        public Wiper GetWiperData(WiperObject wiperObject)
        {
            Wiper wiper;
            wiper.origin = rainScript.m_WindshieldPlane.WorldPosToWindshieldPos(wiperObject.originPos.position);
            Vector2 startPos = rainScript.m_WindshieldPlane.WorldPosToWindshieldPos(wiperObject.startPos.position);
            Vector2 endPos = rainScript.m_WindshieldPlane.WorldPosToWindshieldPos(wiperObject.endPos.position);
            wiper.pos = new Vector4(startPos.x, startPos.y, endPos.x, endPos.y);
            Vector2 prevStartPos = wiperObject.lastStartPos;
            Vector2 prevEndPos = wiperObject.lastEndPos;
            wiper.prevPos = new Vector4(prevStartPos.x, prevStartPos.y, prevEndPos.x, prevEndPos.y);

            return wiper;
        }

        public Wiper[] GetWipersData()
        {
            return wipersData;
        }
    }
}
