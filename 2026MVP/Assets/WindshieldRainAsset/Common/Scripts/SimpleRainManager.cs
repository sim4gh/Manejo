using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace ShadedTechnology.WindshieldRainAsset
{

    [AddComponentMenu("Windshield Rain Asset/Simple Rain Manager")]
    public class SimpleRainManager : MonoBehaviour
{
    public float m_addVelocityFactor = 0.5f;
    public float m_accelerationScale = 1;
    public Material[] m_rainMaterials;
    public Vector3 m_gravityVector = new Vector3(0, -9.8f, 0);
    public float m_timeMod = 4.0f;

    private const float _accelerationScaleMultiplier = 0.01f;
    private const int rain_layers_count = 4;
    private Matrix4x4 _transformMatrix;

    private Vector3 _currentAcceleration;
    private Vector3 _lastPosition;
    private Vector3 _lastVelocity = Vector3.zero;

    [Header("Triplanar Rotation")]
    public Vector3 m_triplanarFacesRotation = Vector3.zero;

    
#if UNITY_EDITOR
    public bool m_DebugMaterial = false;
    public bool m_ShowDebugGizmos = false;
    public float m_GizmoScale = 1.0f;
    public float m_GizmoDistance = 1.0f;

#endif

    public bool m_SetAccelerationForcibly = false;
    public Vector3 m_ForcedAcceleration;

    public void UpdateTriplanarRotation()
    {
        _transformMatrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(-m_triplanarFacesRotation), transform.lossyScale);
        foreach(Material rainMaterial in m_rainMaterials) {
            rainMaterial.SetMatrix("_TransformMatrix", _transformMatrix);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        _lastPosition = transform.position;
        _currentAcceleration = m_gravityVector;
        Vector3 acceleration = transform.worldToLocalMatrix.MultiplyVector(_currentAcceleration);

        _transformMatrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(-m_triplanarFacesRotation), transform.lossyScale);//  CreateBasisTransformMatrix(Vector3.right, Vector3.up, Vector3.forward, right.normalized, up.normalized, forward.normalized);
        
        //Matrix4x4 rotationMatrix = Matrix4x4.Rotate(Quaternion.Inverse(transform.rotation));
        //_transformMatrix = rotationMatrix * _transformMatrix;
        
        acceleration = _transformMatrix.MultiplyVector(acceleration);

        foreach(Material rainMaterial in m_rainMaterials) {
            rainMaterial.SetVector("_RainDirection_0", acceleration);
            rainMaterial.SetVector("_RainDirection_1", acceleration);
            rainMaterial.SetVector("_RainDirection_2", acceleration);
            rainMaterial.SetVector("_RainDirection_3", acceleration);
            rainMaterial.SetMatrix("_TransformMatrix", _transformMatrix);
            rainMaterial.SetFloat("_ModTime", m_timeMod);
            //rainMaterial.SetVector("_UpVector", transform.InverseTransformDirection(Vector3.up));
            //rainMaterial.SetVector("_ForwardVector", transform.InverseTransformDirection(Vector3.forward));
            //rainMaterial.SetVector("_RightVector", transform.InverseTransformDirection(Vector3.right));
        }
    }

    private int index = -1;

    void CalculateCurrentPosVelAcc()
    {
        Vector3 velocity = (transform.position - _lastPosition) / Time.fixedDeltaTime;
        _lastPosition = transform.position;
        Vector3 acceleration = -((velocity - _lastVelocity) / Time.fixedDeltaTime);
        _lastVelocity = velocity;
        _currentAcceleration = (m_gravityVector + acceleration) * m_accelerationScale * _accelerationScaleMultiplier;
    }

    void FixedUpdate()
    {
        CalculateCurrentPosVelAcc();
        
        int current_index = (int)( (Time.timeSinceLevelLoad % m_timeMod) / (m_timeMod / (float)(rain_layers_count)));
        if (index < 0) {
            index = current_index;
        }

        if (index == current_index) {
            Vector3 acceleration = _currentAcceleration;
            if (m_SetAccelerationForcibly) {
                acceleration = transform.worldToLocalMatrix.MultiplyVector(m_ForcedAcceleration);
                acceleration = _transformMatrix.MultiplyVector(acceleration);
            } else {
                acceleration = transform.worldToLocalMatrix.MultiplyVector(acceleration - m_addVelocityFactor * _lastVelocity * m_accelerationScale * _accelerationScaleMultiplier);
                acceleration = _transformMatrix.MultiplyVector(acceleration);
            }
            //Debug.Log($"Set vector [{velocity}] _RainDirection_{index}, time is [{Time.timeSinceLevelLoad}]");
            foreach(Material rainMaterial in m_rainMaterials) {
                rainMaterial.SetVector("_RainDirection_" + index, (Vector4)acceleration);
                rainMaterial.SetMatrix("_TransformMatrix", _transformMatrix);
            }
            index = (index + 1) % rain_layers_count;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!m_ShowDebugGizmos) {
            return;
        }

        Vector3 cameraPos = SceneView.currentDrawingSceneView.camera.transform.position;

        float scale = m_GizmoScale;
        float epsilon = 0.001f;
        float distance = m_GizmoDistance;
        Matrix4x4 matrix = Matrix4x4.TRS(transform.position, transform.rotation * Quaternion.Inverse(Quaternion.Euler(-m_triplanarFacesRotation)), Vector3.one);// * Matrix4x4.Rotate(Quaternion.Inverse(Quaternion.Euler(-m_triplanarFacesRotation)));

        List< Tuple< float, Color32, Matrix4x4, Vector3 > > cubeFaces = new();

        Matrix4x4 faceMatrix = matrix * Matrix4x4.Translate(new Vector3(distance, 0, 0));
        float cameraDistance = (cameraPos - faceMatrix.MultiplyPoint(Vector3.zero)).magnitude;
        Vector3 faceDimensions = new Vector3(epsilon, scale, scale);
        cubeFaces.Add(new(cameraDistance, Color.red, faceMatrix, faceDimensions));
		faceMatrix = matrix * Matrix4x4.Translate(new Vector3(-distance, 0, 0));
        cameraDistance = (cameraPos - faceMatrix.MultiplyPoint(Vector3.zero)).magnitude;
        cubeFaces.Add(new(cameraDistance, Color.red, faceMatrix, faceDimensions));
        
		faceMatrix = matrix * Matrix4x4.Translate(new Vector3(0, distance, 0));
        cameraDistance = (cameraPos - faceMatrix.MultiplyPoint(Vector3.zero)).magnitude;
		faceDimensions = new Vector3(scale, epsilon, scale);
        cubeFaces.Add(new(cameraDistance, Color.green, faceMatrix, faceDimensions));
		faceMatrix = matrix * Matrix4x4.Translate(new Vector3(0, -distance, 0));
        cameraDistance = (cameraPos - faceMatrix.MultiplyPoint(Vector3.zero)).magnitude;
        cubeFaces.Add(new(cameraDistance, Color.green, faceMatrix, faceDimensions));
        
		faceMatrix = matrix * Matrix4x4.Translate(new Vector3(0, 0, distance));
        cameraDistance = (cameraPos - faceMatrix.MultiplyPoint(Vector3.zero)).magnitude;
		faceDimensions = new Vector3(scale, scale, epsilon);
        cubeFaces.Add(new(cameraDistance, Color.blue, faceMatrix, faceDimensions));
		faceMatrix = matrix * Matrix4x4.Translate(new Vector3(0, 0, -distance));
        cameraDistance = (cameraPos - faceMatrix.MultiplyPoint(Vector3.zero)).magnitude;
        cubeFaces.Add(new(cameraDistance, Color.blue, faceMatrix, faceDimensions));

        cubeFaces.Sort((x, y)=> { return y.Item1.CompareTo(x.Item1); });

        foreach ( var face in cubeFaces ) {
            Gizmos.color = face.Item2;
            Gizmos.matrix = face.Item3;
            Gizmos.DrawCube(Vector3.zero, face.Item4);
        }

        
		Gizmos.matrix = Matrix4x4.identity;
		Gizmos.color = Color.white;
	}
#endif

}
}
