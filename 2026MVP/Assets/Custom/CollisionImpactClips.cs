using UnityEngine;

/// <summary>
/// Container de AudioClips de impacto para CollisionFeedback. Se guarda en
/// Resources/Custom/CollisionImpactClips.asset y se popula por inspector con los
/// Impact*.wav del RCCP (Assets/Realistic Car Controller Pro/Audio/Impact/).
/// </summary>
[CreateAssetMenu(fileName = "CollisionImpactClips", menuName = "Tlax/Collision Impact Clips")]
public class CollisionImpactClips : ScriptableObject
{
    public AudioClip[] clips;
}
