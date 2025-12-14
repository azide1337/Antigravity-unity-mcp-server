using UnityEngine;

public class RPGPlayer : MonoBehaviour
{
    public float speed = 5f;

    void Update()
    {
        float moveH = Input.GetAxis("Horizontal");
        float moveV = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(moveH, 0, moveV) * speed * Time.deltaTime;
        transform.Translate(movement, Space.World);
    }
}
