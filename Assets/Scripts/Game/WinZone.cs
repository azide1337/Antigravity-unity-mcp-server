using UnityEngine;

public class WinZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            Debug.Log("YOU WIN! 🎉");
            // Basic "Reset" effect - teleport player back
            other.transform.position = new Vector3(0, 5, 0);
            other.GetComponent<Rigidbody>().velocity = Vector3.zero;
        }
    }
}
