using UnityEditor.Experimental.GraphView;
using UnityEngine;

public enum AsientoOrientacion
{
    Derecho,
    Izquierdo,
    Atras
}
public class Asiento : MonoBehaviour
{
    public bool disponible = false;
    public AsientoOrientacion  _Orientation;
    public void Start()
    {
        disponible = true;
    }

}
