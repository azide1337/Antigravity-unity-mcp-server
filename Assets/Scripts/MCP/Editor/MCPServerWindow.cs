using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

public class MCPServerWindow : EditorWindow
{
    private static Process serverProcess;
    private string serverPath;

    [MenuItem("Tools/Unity MCP Server")]
    public static void ShowWindow()
    {
        GetWindow<MCPServerWindow>("MCP Server");
    }

    private void OnEnable()
    {
        // Calculate path to mcp-server (assumes it is parallel to Assets)
        serverPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../mcp-server"));
    }

    private void OnDisable()
    {
        // Optional: Kill server on window close? Better to leave it running unless explicitly stopped.
    }

    private void OnGUI()
    {
        GUILayout.Label("Unity MCP Server Control", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        if (serverProcess == null || serverProcess.HasExited)
        {
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Start Server", GUILayout.Height(40)))
            {
                StartServer();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Server is NOT running.", MessageType.Info);
        }
        else
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Stop Server", GUILayout.Height(40)))
            {
                StopServer();
                return;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox($"Server running. PID: {serverProcess.Id}", MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Server Path:", EditorStyles.miniLabel);
        EditorGUILayout.SelectableLabel(serverPath, EditorStyles.textField, GUILayout.Height(20));
    }

    private void StartServer()
    {
        if (!Directory.Exists(serverPath))
        {
            UnityEngine.Debug.LogError($"MCP Server folder not found at: {serverPath}");
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
#if UNITY_EDITOR_WIN
        startInfo.FileName = "cmd.exe";
        startInfo.Arguments = "/c npm start";
#else
        startInfo.FileName = "/bin/bash";
        startInfo.Arguments = "-c \"npm start\"";
#endif
        startInfo.WorkingDirectory = serverPath;
        startInfo.WorkingDirectory = serverPath;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true; // Hide the black window
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        
        try
        {
            serverProcess = Process.Start(startInfo);
            
            // Redirect logs to Unity Console
            serverProcess.OutputDataReceived += (sender, args) => {
                if (!string.IsNullOrEmpty(args.Data)) UnityEngine.Debug.Log($"[MCP] {args.Data}");
            };
            serverProcess.ErrorDataReceived += (sender, args) => {
                if (!string.IsNullOrEmpty(args.Data)) UnityEngine.Debug.LogError($"[MCP Error] {args.Data}");
            };

            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();

            UnityEngine.Debug.Log("MCP Server started in background.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to start server: {e.Message}");
        }
    }

    private void StopServer()
    {
        if (serverProcess != null && !serverProcess.HasExited)
        {
            serverProcess.Kill();
            serverProcess = null;
            UnityEngine.Debug.Log("MCP Server stopped.");
        }
    }
}
