using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace ShadedTechnology.WindshieldRainAsset
{
    struct Droplet
    {
        public Vector2 position;
        public Vector2 velocity;
        public float size;
    }

    [AddComponentMenu("Windshield Rain Asset/WindshieldRain")]
    public class WindshieldRain : MonoBehaviour
    {
        [HideInInspector]
        public const int MAX_DROPLETS_IN_CELL = 5;
        [HideInInspector]
        public const int NUM_THREADS_PER_GROUP = 16;

        [SerializeField] public WindshieldPlane m_WindshieldPlane;

        //[Header("Shader Settings")]
        public Vector2Int m_Resolution = new Vector2Int(2048, 1024);
        public Vector2Int m_GridDimensions = new Vector2Int(256, 128);
        public int m_MaxDropletsInCell = 5;
        public ComputeShader m_ComputeShader;

        //[Space]
        //[Header("Turbulance Texture Settings")]
        public bool m_EnableTurbulance = true;
        public Texture m_TurbulanceTexture;
        public Vector2 m_TurbulanceScale = new Vector2(1, 1);
        public float m_TurbulanceImpact = 0.7f;
        public float m_TurbulanceSpeedImpactMultiplier = 1;
        [Range(0, 1)]
        public float m_MaxTurbulanceSpeedImpact = 0.1f;

        //[Space]
        //[Header("Rain Drops Settings")]
        public Vector2 m_Movement;
        public Vector2 Movement
        {
            set
            {
                m_Movement = value;
                if (m_ComputeShader != null)
                {
                    m_ComputeShader.SetVector("_MovementVector", m_Movement);
                }
            }
            get
            {
                return m_Movement;
            }
        }
        public float m_MaxDropletRadius = 0.2f;
        public float m_MinDropletRadius = 0.5f;
        public float m_MaxDropVelocity = 1;
        [Range(0, 10000)]
        public float m_SpawnRate = 500;
        [Range(0, 100)]
        public float m_SpawnAmount = 0.3f;
        [Range(0, 1)]
        public float m_Drag = 0.2f;
        public float m_SpeedMultiplier = 1;
        public float m_StartDropsVelocity = 1.0f;
        [Range(0, 10)]
        public float m_RainStreakDecayRate = 1.0f;
        public bool m_EnableDropsInfluence = true;
        [Range(0, 1)]
        public float m_DropsInfluenceDistance = 1.0f;
        [Range(0, 1)]
        public float m_DropsInfluenceProportion = 0.5f;
        //[Range(0, 0.1f)]
        public float m_DropsInfluenceAddition = 0.2f;

        //[Space]
        //[Header("Wipers Settings")]
        public bool m_WipersEnabled = true;
        public Wipers m_WipersScript;
        [Range(0, 1)]
        public float m_WipersThreshold = 0.9f;
        public float m_WipersSpeedMultiplier = 2;

        //[Space]
        //[Header("Rain Material")]
        public bool m_SetRainMaterialTexture = true;
        public Material m_RainMaterial;

        //[Space]
        //[HideInInspector]
        public RainPostProcessProfile m_RainPostProcessProfile;

        private int _kernelInitIndex;
        private int _kernelUpdateIndex;
        //private int _kernelRelocateDrops;
        private int _kernelToTexture;
        private int _kernelInitTextures;
        ComputeBuffer _rainGridBuffer;
        ComputeBuffer _rainGridBuffer2;
        ComputeBuffer _rainCountBuffer;
        ComputeBuffer _rainCountBuffer2;
        ComputeBuffer _wipersBuffer;
        private RenderTexture _renderTexture1;
        private RenderTexture _renderTexture2;

        [SerializeField] private float _fixedDeltaTime = 0.01f;
        private float _lastDeltaTime;
        private float _lastUpdateTime = 0f;

#if UNITY_EDITOR
        [HideInInspector] public bool _postProcessProfileFoldout = true;
        [HideInInspector] public bool _windshieldPlaneSettingsFoldout = true;
        [HideInInspector] public bool _textureSettingsFoldout = true;
        [HideInInspector] public bool _turbulanceTextureSettingsFoldout = true;
        [HideInInspector] public bool _rainDropsSettingsFoldout = true;
        [HideInInspector] public bool _wipersSettingsFoldout = true;
        [HideInInspector] public bool _rainMaterialFoldout = true;
#endif

        private LocalKeyword _enableTurbulanceKeyword;
        private LocalKeyword _wipersEnabledKeyword;

        private bool _isInitialized = false;

        private void OnDestroy()
        {
            ReleaseBuffers();
        }

        void InitRenderTexture(ref RenderTexture renderTexture)
        {
            renderTexture = new RenderTexture(m_Resolution.x, m_Resolution.y, 0, RenderTextureFormat.ARGBFloat);
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
            renderTexture.filterMode = FilterMode.Point;
        }

        void InitializeBuffer(ref ComputeBuffer buffer)
        {
            buffer = new ComputeBuffer(
                m_GridDimensions.x * m_GridDimensions.y * m_MaxDropletsInCell, // number of elements in the buffer
                Marshal.SizeOf(typeof(Droplet)) // size of each element
            );
        }

        void InitializeCountBuffer(ref ComputeBuffer buffer)
        {
            buffer = new ComputeBuffer(
                m_GridDimensions.x * m_GridDimensions.y, // number of elements in the buffer
                Marshal.SizeOf(typeof(int)) // size of each element
            );
        }

        void InitializeWipersBuffer(ref ComputeBuffer buffer)
        {
            if (!m_WipersEnabled || m_WipersScript == null || m_WipersScript.wipers.Length == 0)
            {
                m_ComputeShader.SetInt("_WipersCount", 0);
                return;
            }

            buffer = new ComputeBuffer(m_WipersScript.wipers.Length, // number of elements in the buffer
                Marshal.SizeOf(typeof(Wiper)) // size of each element
            );
            m_ComputeShader.SetInt("_WipersCount", m_WipersScript.wipers.Length);
        }

        void InitKernelIndicies()
        {
            _kernelInitIndex = m_ComputeShader.FindKernel("CSInit");
            _kernelUpdateIndex = m_ComputeShader.FindKernel("CSUpdatePhysics");
            //_kernelRelocateDrops = m_ComputeShader.FindKernel("CSRelocateDrops");
            _kernelToTexture = m_ComputeShader.FindKernel("CSToTexture");
            _kernelInitTextures = m_ComputeShader.FindKernel("CSInitTextures");
        }

        void DispatchInitTextures()
        {
            int numGroupsRenderX = Mathf.CeilToInt(m_Resolution.x / (float)NUM_THREADS_PER_GROUP);
            int numGroupsRenderY = Mathf.CeilToInt(m_Resolution.y / (float)NUM_THREADS_PER_GROUP);
            m_ComputeShader.SetTexture(_kernelInitTextures, "Result", _renderTexture1);
            m_ComputeShader.SetTexture(_kernelInitTextures, "PrevState", _renderTexture2);
            m_ComputeShader.Dispatch(_kernelInitTextures, numGroupsRenderX, numGroupsRenderY, 1);
        }

        void DispatchInitBuffers()
        {
            m_ComputeShader.SetInt("_MaxDropletsInCell", m_MaxDropletsInCell);
            m_ComputeShader.SetBuffer(_kernelInitIndex, "_CountInBuffer", _rainCountBuffer);
            m_ComputeShader.SetBuffer(_kernelInitIndex, "_CountOutBuffer", _rainCountBuffer2);
            m_ComputeShader.SetBuffer(_kernelInitIndex, "InBuffer", _rainGridBuffer);
            m_ComputeShader.SetBuffer(_kernelInitIndex, "OutBuffer", _rainGridBuffer2);
            m_ComputeShader.SetInts("_Resolution", new int[] { m_Resolution.x, m_Resolution.y });
            m_ComputeShader.SetFloats("_WindshieldPlaneSize", new float[] { m_WindshieldPlane.width, m_WindshieldPlane.height });
            m_ComputeShader.SetInts("_CellsCount2d", new int[] { m_GridDimensions.x, m_GridDimensions.y });
            _enableTurbulanceKeyword = new LocalKeyword(m_ComputeShader, "TURBULANCE_ENABLED");
            _wipersEnabledKeyword = new LocalKeyword(m_ComputeShader, "WIPERS_ENABLED");
            m_ComputeShader.SetKeyword(_enableTurbulanceKeyword, m_EnableTurbulance && m_TurbulanceTexture != null);
            m_ComputeShader.SetKeyword(_wipersEnabledKeyword, m_WipersEnabled && m_WipersScript != null);
            if (m_EnableTurbulance && m_TurbulanceTexture != null)
            {
                m_ComputeShader.SetTexture(_kernelUpdateIndex, "_TurbulanceTexture", m_TurbulanceTexture);
                m_ComputeShader.SetInts("_TurbulanceResolution", new int[] { m_TurbulanceTexture.width, m_TurbulanceTexture.height });
            }
            int numGroupsX = Mathf.CeilToInt(m_GridDimensions.x / (float)NUM_THREADS_PER_GROUP);
            int numGroupsY = Mathf.CeilToInt(m_GridDimensions.y / (float)NUM_THREADS_PER_GROUP);
            m_ComputeShader.Dispatch(_kernelInitIndex, numGroupsX, numGroupsY, 1);
        }

        public void ResetRain()
        {
            if (!_isInitialized)
            {
                return;
            }

            // Initialize buffer
            ReleaseBuffers();
            InitializeBuffer(ref _rainGridBuffer);
            InitializeBuffer(ref _rainGridBuffer2);
            InitializeCountBuffer(ref _rainCountBuffer);
            InitializeCountBuffer(ref _rainCountBuffer2);
            InitializeWipersBuffer(ref _wipersBuffer);

            // Initialize the ResultTexture
            ReleaseRenderTextures();
            InitRenderTexture(ref _renderTexture1);
            InitRenderTexture(ref _renderTexture2);

            // Dispatch initialization kernels
            DispatchInitTextures();
            DispatchInitBuffers();
        }

        // Start is called before the first frame update
        void Start()
        {
            if (m_EnableTurbulance && m_TurbulanceTexture == null)
            {
                Debug.LogError("Turbulance is enabled, but TurbulanceTexture is not set");
            }
            if (m_WipersEnabled && m_WipersScript == null)
            {
                Debug.LogError("Wipers are enabled, but WipersScript is not set");
            }

            m_ComputeShader = Instantiate(m_ComputeShader);
            if (m_RainPostProcessProfile != null)
            {
                m_RainPostProcessProfile = Instantiate(m_RainPostProcessProfile);
            }

            InitKernelIndicies();
            _isInitialized = true;

            ResetRain();

            if (m_RainPostProcessProfile != null)
            {
                m_RainPostProcessProfile.InitPostProcesses(m_Resolution);
            }

            UpdateShaderValues();
        }

        bool bufferSwap = false;
        bool textureSwap = false;

        ComputeBuffer GetBuffer(int id)
        {
            if ((!bufferSwap && id == 0) || (bufferSwap && id == 1))
            {
                return _rainGridBuffer;
            }
            return _rainGridBuffer2;
        }

        ComputeBuffer GetCountBuffer(int id)
        {
            if ((!bufferSwap && id == 0) || (bufferSwap && id == 1))
            {
                return _rainCountBuffer;
            }
            return _rainCountBuffer2;
        }


        RenderTexture GetPrevTexture()
        {
            return (textureSwap ? _renderTexture1 : _renderTexture2);
        }

        public RenderTexture GetCurrTexture()
        {
            return (textureSwap ? _renderTexture2 : _renderTexture1);
        }

        void ReleaseBuffers()
        {
            if (_rainGridBuffer != null)
            {
                _rainGridBuffer.Release();
            }
            if (_rainGridBuffer2 != null)
            {
                _rainGridBuffer2.Release();
            }
            if (_rainCountBuffer != null)
            {
                _rainCountBuffer.Release();
            }
            if (_rainCountBuffer2 != null)
            {
                _rainCountBuffer2.Release();
            }
            if (_wipersBuffer != null)
            {
                _wipersBuffer.Release();
            }
        }

        void ReleaseRenderTextures()
        {
            if (_renderTexture1 != null)
            {
                _renderTexture1.Release();
                Destroy(_renderTexture1);
                _renderTexture1 = null;
            }
            if (_renderTexture2 != null)
            {
                _renderTexture2.Release();
                Destroy(_renderTexture2);
                _renderTexture2 = null;
            }
        }

        public void UpdateShaderValues()
        {
            if (m_ComputeShader == null)
            {
                return;
            }

            m_ComputeShader.SetFloat("_MaxVelocity", m_MaxDropVelocity);
            m_ComputeShader.SetVector("_MovementVector", m_Movement);
            if (m_WindshieldPlane == null)
            {
                m_WindshieldPlane = new WindshieldPlane(this);
            }
            float maxDropSize = Mathf.Min(m_WindshieldPlane.width / m_GridDimensions.x, m_WindshieldPlane.height / m_GridDimensions.y);
            m_ComputeShader.SetFloat("_MaxPossibleDropletSize", maxDropSize);
            m_ComputeShader.SetFloat("_MaxDropletSize", m_MaxDropletRadius * maxDropSize);
            m_ComputeShader.SetFloat("_MinDropletSize", m_MinDropletRadius * maxDropSize);
            m_ComputeShader.SetFloat("_SpawnRate", m_SpawnRate);
            m_ComputeShader.SetFloat("_SpawnThreshold", m_SpawnAmount);
            m_ComputeShader.SetFloat("_StartVelocity", m_StartDropsVelocity);
            m_ComputeShader.SetFloat("_Drag", m_Drag);
            m_ComputeShader.SetFloat("_SpeedMultiplier", m_SpeedMultiplier);
            if (m_EnableTurbulance)
            {
                m_ComputeShader.SetFloat("_MaxTurbulanceSpeedImpact", m_MaxTurbulanceSpeedImpact);
                m_ComputeShader.SetFloats("_TurbulanceScale", new float[] { m_TurbulanceScale.x, m_TurbulanceScale.y });
                m_ComputeShader.SetFloat("_TurbulanceImpact", m_TurbulanceImpact);
                m_ComputeShader.SetFloat("_TurbulanceSpeedImpact", m_TurbulanceSpeedImpactMultiplier);
            }
            m_DropsInfluenceDistance = Mathf.Min(Mathf.Max(0, m_DropsInfluenceDistance), 1.0f);
            m_ComputeShader.SetFloat("_DropsInfluenceDistance", m_EnableDropsInfluence ? Mathf.Max(0, m_DropsInfluenceDistance * maxDropSize) : 0);
            m_ComputeShader.SetFloat("_DropsInfluenceProportion", m_DropsInfluenceProportion);
            m_ComputeShader.SetFloat("_DropsInfluenceAddition", m_DropsInfluenceAddition);
            m_ComputeShader.SetFloat("_RainStreakDecayRate", m_RainStreakDecayRate);
            m_ComputeShader.SetFloat("_WipersThreshold", m_WipersThreshold);
            m_ComputeShader.SetFloat("_WipersSpeed", m_WipersSpeedMultiplier);
        }

        void UpdateDropletsBuffers()
        {
            int numGroupsX = Mathf.CeilToInt(m_GridDimensions.x / (float)NUM_THREADS_PER_GROUP);
            int numGroupsY = Mathf.CeilToInt(m_GridDimensions.y / (float)NUM_THREADS_PER_GROUP);

            m_ComputeShader.SetFloat("_Time", Time.time);
            m_ComputeShader.SetFloat("_DeltaTime", _lastDeltaTime);
            m_ComputeShader.SetBuffer(_kernelUpdateIndex, "InBuffer", GetBuffer(0));
            m_ComputeShader.SetBuffer(_kernelUpdateIndex, "OutBuffer", GetBuffer(1));
            m_ComputeShader.SetBuffer(_kernelUpdateIndex, "_CountInBuffer", GetCountBuffer(0));
            m_ComputeShader.SetBuffer(_kernelUpdateIndex, "_CountOutBuffer", GetCountBuffer(1));
            if (m_WipersEnabled)
            {
                if (m_WipersScript && _wipersBuffer != null && m_WipersScript.IsInitialized())
                {
                    m_ComputeShader.SetTexture(_kernelUpdateIndex, "WipersTexture", m_WipersScript.getCurrTexture());
                    var wipersData = m_WipersScript.GetWipersData();
                    if (wipersData != null)
                    {
                        _wipersBuffer.SetData(m_WipersScript.GetWipersData());
                    }
                    m_ComputeShader.SetBuffer(_kernelUpdateIndex, "_Wipers", _wipersBuffer);
                }
                else
                {
                    return; // Dispatching with wipers enabled but with no wipers data will cause errors
                }
            }
            m_ComputeShader.Dispatch(_kernelUpdateIndex, numGroupsX, numGroupsY, 1);
        }

        void UpdateComputeTexture()
        {
            int numGroupsRenderX = Mathf.CeilToInt(m_Resolution.x / (float)NUM_THREADS_PER_GROUP);
            int numGroupsRenderY = Mathf.CeilToInt(m_Resolution.y / (float)NUM_THREADS_PER_GROUP);
            if (m_WipersEnabled)
            {
                if (m_WipersScript && _wipersBuffer != null && m_WipersScript.IsInitialized())
                {
                    m_ComputeShader.SetBuffer(_kernelToTexture, "_Wipers", _wipersBuffer);
                    m_ComputeShader.SetTexture(_kernelToTexture, "WipersTexture", m_WipersScript.getCurrTexture());
                }
                else
                {
                    return; // Dispatching with wipers enabled but with no wipers data will cause errors
                }
            }
            m_ComputeShader.SetBuffer(_kernelToTexture, "OutBuffer", GetBuffer(1));
            m_ComputeShader.SetTexture(_kernelToTexture, "Result", GetCurrTexture());
            m_ComputeShader.SetTexture(_kernelToTexture, "PrevState", GetPrevTexture());
            m_ComputeShader.Dispatch(_kernelToTexture, numGroupsRenderX, numGroupsRenderY, 1);
        }

        void SwapTexturesAndBuffers()
        {
            bufferSwap = !bufferSwap;
            textureSwap = !textureSwap;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UpdateShaderValues();
        }
#endif

        // Update is called once per frame
        void Update()
        {
            if (Time.time - _lastUpdateTime < _fixedDeltaTime) return;
            _lastDeltaTime = Time.time - _lastUpdateTime;
            _lastUpdateTime = Time.time;

            if (m_WipersEnabled && m_WipersScript.IsInitialized())
            {
                m_WipersScript?.UpdateWipers(_lastDeltaTime);
            }
            UpdateDropletsBuffers();
            UpdateComputeTexture();

            Texture rainTexture;
            if (m_RainPostProcessProfile != null)
            {
                rainTexture = m_RainPostProcessProfile.UpdatePostProcesses(GetCurrTexture());
            }
            else
            {
                rainTexture = GetCurrTexture();
            }
            if (m_SetRainMaterialTexture && m_RainMaterial != null)
            {
                m_RainMaterial.SetTexture("_HeightMap", rainTexture);
            }

            SwapTexturesAndBuffers();
        }
    }

}
