using UnityEngine;

public class LevelFixer : MonoBehaviour
{
    void Start()
    {
        // Fix Plane
        var planes = FindObjectsOfType<MeshFilter>();
        foreach(var p in planes) {
            if(p.name == "MCP_Plane") {
                p.name = "Ground";
                p.transform.position = Vector3.zero;
                p.transform.localScale = new Vector3(2, 1, 10); // Long road
            }
        }

        // Fix Player
        var player = GameObject.Find("MCP_Sphere");
        if(player != null) {
            player.name = "Player";
            player.transform.position = new Vector3(0, 1, -8);
            if(player.GetComponent<Rigidbody>() == null) player.AddComponent<Rigidbody>();
            if(player.GetComponent<PlayerController>() == null) player.AddComponent<PlayerController>();
        }

        // Fix Cubes
        var cubes = FindObjectsOfType<BoxCollider>();
        int count = 0;
        foreach(var c in cubes) {
            if(c.name == "MCP_Cube") {
                if(count == 0) {
                    c.name = "Obstacle";
                    c.transform.position = new Vector3(0, 0.5f, -2);
                    c.GetComponent<BoxCollider>().isTrigger = false;
                } else {
                    c.name = "WinZone";
                    c.transform.position = new Vector3(0, 0.5f, 5);
                    c.GetComponent<BoxCollider>().isTrigger = true;
                    if(c.GetComponent<WinZone>() == null) c.gameObject.AddComponent<WinZone>();
                    // Make it green
                    var rend = c.GetComponent<Renderer>();
                    if(rend) rend.material.color = Color.green;
                }
                count++;
            }
        }
        
        Debug.Log("Level Fixed by MCP! 🔧");
        DestroyImmediate(this); // Clean up
    }
}
