using UnityEngine;

public class Joint_ms : MonoBehaviour
{
    public Joint_ms m_child;
    public Joint_ms GetChild()
    {
        return m_child;
    }

    public void Rotate(float _angle)
    {
        transform.Rotate(Vector3.up * _angle);
    }
}
