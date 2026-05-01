using UnityEngine;

/// <summary>
/// Container de AudioClips de "rechino" de caja para GearGrindingFeedback. Se guarda
/// en Resources/Custom/GearGrindingClips.asset y se popula por inspector con WAVs
/// CC0 descargados (típicamente 2-3 clips cortos &lt;=1s desde Pixabay grinding-gears).
/// </summary>
[CreateAssetMenu(fileName = "GearGrindingClips", menuName = "Tlax/Gear Grinding Clips")]
public class GearGrindingClips : ScriptableObject
{
    public AudioClip[] clips;
}
