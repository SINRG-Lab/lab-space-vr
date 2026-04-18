using UnityEngine;

public class AutoRotate : MonoBehaviour
{
    public Vector3 axis = new Vector3(0, 1, 0);
    public float speed = 60f;

    void Update()
    {
        transform.Rotate(axis, speed * Time.deltaTime);
    }
}