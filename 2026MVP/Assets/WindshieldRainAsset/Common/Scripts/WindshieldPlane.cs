using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{
    [System.Serializable]
    public class WindshieldPlane
    {
        [SerializeField] public float width = 1;
        [SerializeField] public float height = 1;

#if UNITY_EDITOR
        [SerializeField] public bool fixAspectRatio = true;
        [SerializeField] public MeshFilter windshieldMesh;
#endif
        [SerializeField] public WindshieldRain m_WindshieldRain;

        public WindshieldPlane(WindshieldRain rainScript)
        {
            m_WindshieldRain = rainScript;
        }

        public Vector2 WorldPosToWindshieldUV(Vector3 worldPos)
        {
            Vector3 localPos = m_WindshieldRain.transform.InverseTransformPoint(worldPos);
            float x = (localPos.x + (width * 0.5f)) / width;
            float y = (localPos.y + (height * 0.5f)) / height;
            return new Vector2(x, y);
        }

        public Vector2 WorldPosToWindshieldPos(Vector3 worldPos)
        {
            Vector3 localPos = m_WindshieldRain.transform.InverseTransformPoint(worldPos);
            float x = localPos.x + (width * 0.5f);
            float y = localPos.y + (height * 0.5f);
            return new Vector2(x, y);
        }

#if UNITY_EDITOR
        public Mesh RecalculateMeshUVs()
        {
            Mesh mesh = GameObject.Instantiate(windshieldMesh.sharedMesh);

            List<Vector2> UVs = new List<Vector2>();
            for(int i = 0; i < mesh.vertices.Length; ++i)
            {
                Vector2 uv = WorldPosToWindshieldUV(windshieldMesh.transform.TransformPoint(mesh.vertices[i]));
                UVs.Add(uv);
                //Debug.Log(uv);
            }
            mesh.SetUVs(0, UVs);

            return mesh;
        }
#endif
    }
}
