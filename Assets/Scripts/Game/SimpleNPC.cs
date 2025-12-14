using UnityEngine;

public class SimpleNPC : MonoBehaviour
{
    private Transform target;

    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            target = player.transform;
        }
    }

    void Update()
    {
        if (target == null)
        {
             GameObject player = GameObject.FindWithTag("Player");
             if (player != null) target = player.transform;
        }

        if (target != null)
        {
            transform.LookAt(target);
        }
    }
}
