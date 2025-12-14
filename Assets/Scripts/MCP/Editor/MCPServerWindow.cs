using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

[InitializeOnLoad]
public class MCPServerWindow : EditorWindow
{
    private static Process serverProcess;
    private static string serverPath;
    private const string PID_PREF_KEY = "MCP_Server_PID";

    static MCPServerWindow()
    {
        // Called on Unity load and after every compilation
        EditorApplication.delayCall += TryAutoStart;
    }

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

    private static void TryAutoStart()
    {
        serverPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../mcp-server"));
        
        int savedPid = EditorPrefs.GetInt(PID_PREF_KEY, -1);
        if (savedPid != -1)
        {
            try
            {
                var proc = Process.GetProcessById(savedPid);
                if (!proc.HasExited)
                {
                    serverProcess = proc;
                    UnityEngine.Debug.Log($"[MCP] Re-attached to existing server (PID: {savedPid})");
                    HookEvents(serverProcess);
                    return;
                }
            }
            catch
            {
                // Process not found or other error, clear pref and restart
                EditorPrefs.DeleteKey(PID_PREF_KEY);
            }
        }

        // If we fall through here, and IT'S NOT RUNNING, start a new one
        if (serverProcess == null || serverProcess.HasExited)
        {
            StartServerStatic();
        }
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
                StartServerStatic();
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

    private static void StartServerStatic()
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
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true; // Hide the black window
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardInput = true; // Required to keep Node process alive
        
        try
        {
            serverProcess = Process.Start(startInfo);
            EditorPrefs.SetInt(PID_PREF_KEY, serverProcess.Id);
            
            HookEvents(serverProcess);

            UnityEngine.Debug.Log("MCP Server started in background.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to start server: {e.Message}");
        }
    }

    private static void HookEvents(Process p)
    {
        p.OutputDataReceived += (sender, args) => {
            if (!string.IsNullOrEmpty(args.Data)) UnityEngine.Debug.Log($"[MCP] {args.Data}");
        };
        p.ErrorDataReceived += (sender, args) => {
            if (!string.IsNullOrEmpty(args.Data)) {
                if (args.Data.Contains("running on stdio")) 
                    UnityEngine.Debug.Log($"[MCP] {args.Data}");
                else 
                    UnityEngine.Debug.LogError($"[MCP Error] {args.Data}");
            }
        };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
    }

    private void StopServer()
    {
        if (serverProcess != null && !serverProcess.HasExited)
        {
            try {
                serverProcess.Kill();
            } catch {}
            serverProcess = null;
            EditorPrefs.DeleteKey(PID_PREF_KEY);
            UnityEngine.Debug.Log("MCP Server stopped.");
        }
    }
}
