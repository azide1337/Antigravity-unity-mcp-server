using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.AI;

// Recompile Trigger
[ExecuteAlways]
public class MCPBridge : MonoBehaviour
{
    private HttpListener listener;
    private Thread listenerThread;
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();
    private bool isRunning = false;

    [SerializeField] private int port = 8080;

    void OnEnable()
    {
        StopServer(); 
        StartServer();
        Application.logMessageReceivedThreaded += HandleLog;
#if UNITY_EDITOR
        EditorApplication.update += Update;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= Update;
#endif
        Application.logMessageReceivedThreaded -= HandleLog;
        StopServer();
    }

    void Update()
    {
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue()?.Invoke();
            }
        }
    }

    private void StartServer()
    {
        if (isRunning) return;
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            listener.Start();
            isRunning = true;
            listenerThread = new Thread(ListenCalls);
            listenerThread.Start();
            Debug.Log($"[MCPBridge] Server started on port {port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MCPBridge] Failed to start server: {e.Message}");
            StopServer();
        }
    }

    private void StopServer()
    {
        isRunning = false;
        if (listener != null) { try { listener.Stop(); listener.Close(); } catch {} listener = null; }
        if (listenerThread != null) { try { listenerThread.Abort(); } catch {} listenerThread = null; }
    }

    private void ListenCalls()
    {
        while (isRunning && listener != null && listener.IsListening)
        {
            try { var result = listener.BeginGetContext(ListenerCallback, listener); result.AsyncWaitHandle.WaitOne(); }
            catch (ThreadAbortException) { return; }
            catch { }
        }
    }

    private void ListenerCallback(IAsyncResult result)
    {
        if (listener == null || !listener.IsListening) return;
        HttpListenerContext context;
        try { context = listener.EndGetContext(result); } catch { return; }

        var request = context.Request;
        var response = context.Response;
        string responseString = "{}";
        int statusCode = 200;

        try
        {
             if (request.HttpMethod == "GET")
            {
                if (request.Url.AbsolutePath == "/hierarchy")
                {
                    responseString = GetHierarchy();
                }
            }
            else if (request.HttpMethod == "POST")
            {
                 using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    var url = request.Url.AbsolutePath;

                    var waitHandle = new AutoResetEvent(false);
                    string localResp = "";
                    
                    EnqueueMain(() => {
                        try
                        {
                            if (url == "/create_primitive")
                            {
                                var json = JsonUtility.FromJson<PrimitiveRequest>(body);
                                var pType = (PrimitiveType)Enum.Parse(typeof(PrimitiveType), json.type, true);
                                var obj = GameObject.CreatePrimitive(pType);
                                
                                string baseName = !string.IsNullOrEmpty(json.name) ? json.name : "MCP_" + json.type;
                                string uniqueName = baseName;
                                int counter = 1;
                                while(GameObject.Find(uniqueName) != null) {
                                    uniqueName = baseName + "_" + counter;
                                    counter++;
                                }
                                obj.name = uniqueName;

#if UNITY_EDITOR
                                Undo.RegisterCreatedObjectUndo(obj, "Create " + obj.name);
#endif
                                localResp = "{\"status\":\"created\", \"name\":\"" + obj.name + "\"}";
                            }
                            else if (url == "/transform")
                            {
                                var json = JsonUtility.FromJson<TransformRequest>(body);
                                var obj = GameObject.Find(json.name);
                                if (obj != null) {
#if UNITY_EDITOR
                                    Undo.RecordObject(obj.transform, "Move " + obj.name);
#endif
                                    obj.transform.position = new Vector3(json.x, json.y, json.z);
                                    localResp = "{\"status\":\"moved\", \"name\":\"" + json.name + "\"}";
                                } else { localResp = "{\"error\":\"Object not found\"}"; statusCode = 404; }
                            }
                            else if (url == "/scale")
                            {
                                var json = JsonUtility.FromJson<TransformRequest>(body); // Reusing TransformRequest for x,y,z
                                var obj = GameObject.Find(json.name);
                                if (obj != null) {
#if UNITY_EDITOR
                                    Undo.RecordObject(obj.transform, "Scale " + obj.name);
#endif
                                    obj.transform.localScale = new Vector3(json.x, json.y, json.z);
                                    localResp = "{\"status\":\"scaled\", \"name\":\"" + json.name + "\"}";
                                } else { localResp = "{\"error\":\"Object not found\"}"; statusCode = 404; }
                            }
                            else if (url == "/rotation")
                            {
                                var json = JsonUtility.FromJson<TransformRequest>(body);
                                var obj = GameObject.Find(json.name);
                                if (obj != null) {
#if UNITY_EDITOR
                                    Undo.RecordObject(obj.transform, "Rotate " + obj.name);
#endif
                                    obj.transform.localEulerAngles = new Vector3(json.x, json.y, json.z);
                                    localResp = "{\"status\":\"rotated\", \"name\":\"" + json.name + "\"}";
                                } else { localResp = "{\"error\":\"Object not found\"}"; statusCode = 404; }
                            }
                            else if (url == "/parent")
                            {
                                var json = JsonUtility.FromJson<ParentRequest>(body);
                                var obj = GameObject.Find(json.name);
                                var parent = GameObject.Find(json.parentName);
                                if (obj != null && parent != null) {
#if UNITY_EDITOR
                                    Undo.SetTransformParent(obj.transform, parent.transform, "Parent " + obj.name);
#else
                                    obj.transform.SetParent(parent.transform);
#endif
                                    localResp = "{\"status\":\"parented\", \"child\":\"" + json.name + "\", \"parent\":\"" + json.parentName + "\"}";
                                } else { localResp = "{\"error\":\"Object or Parent not found\"}"; statusCode = 404; }
                            }
                            else if (url == "/instantiate")
                            {
                                var json = JsonUtility.FromJson<InstantiateRequest>(body);
                                UnityEngine.Object prefab = Resources.Load(json.path);
#if UNITY_EDITOR
                                if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(json.path);
#endif
                                if (prefab != null) {
                                    GameObject go;
#if UNITY_EDITOR
                                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
#else
                                    go = (GameObject)Instantiate(prefab);
#endif
                                    if(!string.IsNullOrEmpty(json.name)) go.name = json.name;
#if UNITY_EDITOR
                                    Undo.RegisterCreatedObjectUndo(go, "Instantiate " + go.name);
#endif
                                    localResp = "{\"status\":\"created\", \"name\":\"" + go.name + "\"}";
                                } else { localResp = "{\"error\":\"Prefab not found at " + json.path + "\"}"; statusCode = 404; }
                            }
                            else if (url == "/screenshot")
                            {
                                // Vision: Capture GameView
                                Texture2D screenTex = ScreenCapture.CaptureScreenshotAsTexture();
                                if (screenTex != null) {
                                    byte[] bytes = screenTex.EncodeToJPG(75); // JPG is smaller/faster than PNG
                                    string base64 = System.Convert.ToBase64String(bytes);
                                    Destroy(screenTex);
                                    localResp = "{\"status\":\"success\", \"image\":\"" + base64 + "\"}";
                                } else {
                                    localResp = "{\"error\":\"Failed to capture screenshot. Is Game View active?\"}";
                                    statusCode = 500;
                                }
                            }
                            else if (url == "/logs")
                            {
                                // Hearing: Return buffered logs
                                string joinedLogs = "";
                                lock(logLock) {
                                    joinedLogs = string.Join("\\n", logBuffer).Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
                                    logBuffer.Clear(); 
                                }
                                localResp = "{\"status\":\"success\", \"logs\":\"" + joinedLogs + "\"}";
                            }
                            else if (url == "/create_script")
                            {
                                // Creation: Write C# file
                                var json = JsonUtility.FromJson<ScriptRequest>(body);
                                string folder = Path.Combine(Application.dataPath, "Scripts", "Generated");
                                if(!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                                
                                string filePath = Path.Combine(folder, json.fileName);
                                File.WriteAllText(filePath, json.code);
                                
#if UNITY_EDITOR
                                AssetDatabase.Refresh(); // Trigger compilation
#endif
                                localResp = "{\"status\":\"created\", \"path\":\"" + filePath + "\"}";
                            }
                            else if (url == "/invoke")
                            {
                                var json = JsonUtility.FromJson<InvokeRequest>(body);
                                var obj = GameObject.Find(json.gameObjectName);
                                if (obj != null) {
                                    var comp = obj.GetComponent(json.componentName);
                                    if(comp != null) {
                                        var method = comp.GetType().GetMethod(json.methodName);
                                        if(method != null) {
                                            object[] paramsArray = null;
                                            if(!string.IsNullOrEmpty(json.parameter)) {
                                                // Basic single parameter parsing
                                                var pType = method.GetParameters()[0].ParameterType;
                                                object val = json.parameter;
                                                try {
                                                    if(pType == typeof(int)) val = int.Parse(json.parameter);
                                                    else if(pType == typeof(float)) val = float.Parse(json.parameter);
                                                    else if(pType == typeof(bool)) val = bool.Parse(json.parameter);
                                                } catch {}
                                                paramsArray = new object[] { val };
                                            }
                                            method.Invoke(comp, paramsArray);
                                            localResp = "{\"status\":\"invoked\", \"method\":\"" + json.methodName + "\"}";
                                        } else { localResp = "{\"error\":\"Method not found\"}"; statusCode = 404; }
                                    } else { localResp = "{\"error\":\"Component not found\"}"; statusCode = 404; }
                                } else { localResp = "{\"error\":\"Object not found\"}"; statusCode = 404; }
                            }
                            else if (url == "/object_delete") {
                                var json = JsonUtility.FromJson<TagRequest>(body); // reusing TagRequest for 'name'
                                var obj = GameObject.Find(json.name);
                                if(obj!=null) {
#if UNITY_EDITOR
                                    Undo.DestroyObjectImmediate(obj);
#else
                                    Destroy(obj);
#endif
                                    localResp = "{\"status\":\"deleted\", \"name\":\"" + json.name + "\"}";
                                } else { localResp = "{\"error\":\"Object not found\"}"; statusCode=404; }
                            }
                            else if (url == "/tag_add")
                            {
                                var json = JsonUtility.FromJson<TagRequest>(body);
#if UNITY_EDITOR
                                SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                                SerializedProperty tagsProp = tagManager.FindProperty("tags");
                                
                                bool found = false;
                                for (int i = 0; i < tagsProp.arraySize; i++)
                                {
                                    SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
                                    if (t.stringValue.Equals(json.tag)) { found = true; break; }
                                }
                                
                                if (!found)
                                {
                                    tagsProp.InsertArrayElementAtIndex(0);
                                    SerializedProperty n = tagsProp.GetArrayElementAtIndex(0);
                                    n.stringValue = json.tag;
                                    tagManager.ApplyModifiedProperties();
                                    localResp = "{\"status\":\"created\", \"tag\":\"" + json.tag + "\"}";
                                }
                                else
                                {
                                    localResp = "{\"status\":\"exists\", \"tag\":\"" + json.tag + "\"}";
                                }
#else
                                localResp = "{\"error\":\"Editor Only feature\"}";
#endif
                            }
                            else if (url == "/physics/cast")
                            {
                                // Spatial: Physics Query
                                var json = JsonUtility.FromJson<PhysicsRequest>(body);
                                var origin = GameObject.Find(json.origin)?.transform.position ?? Vector3.zero;
                                if(json.useVectorOrigin) origin = new Vector3(json.originX, json.originY, json.originZ);
                                
                                List<string> hits = new List<string>();
                                
                                if(json.type == "ray") {
                                    Vector3 dir = new Vector3(json.dirX, json.dirY, json.dirZ);
                                    if(dir == Vector3.zero) dir = GameObject.Find(json.origin)?.transform.forward ?? Vector3.forward;
                                    
                                    RaycastHit[] results = Physics.RaycastAll(origin, dir, json.maxDistance > 0 ? json.maxDistance : 100f);
                                    foreach(var h in results) {
                                        hits.Add($"{{\"name\": \"{h.collider.name}\", \"distance\": {h.distance}, \"point\": \"{h.point}\", \"tag\": \"{h.collider.tag}\"}}");
                                    }
                                }
                                else if (json.type == "sphere") {
                                    Collider[] results = Physics.OverlapSphere(origin, json.radius > 0 ? json.radius : 5f);
                                    foreach(var c in results) {
                                         hits.Add($"{{\"name\": \"{c.name}\", \"distance\": {Vector3.Distance(origin, c.transform.position)}, \"point\": \"{c.transform.position}\", \"tag\": \"{c.tag}\"}}");
                                    }
                                }
                                localResp = "{\"status\":\"success\", \"hits\":[" + string.Join(",", hits) + "]}";
                            }
                            else if (url == "/navmesh/path")
                            {
                                // Spatial: Navigation
                                var json = JsonUtility.FromJson<NavMeshRequest>(body);
                                var start = GameObject.Find(json.from)?.transform.position ?? Vector3.zero;
                                var end = GameObject.Find(json.to)?.transform.position ?? Vector3.zero;
                                if(json.useVectors) {
                                    start = new Vector3(json.startX, json.startY, json.startZ);
                                    end = new Vector3(json.endX, json.endY, json.endZ);
                                }

                                NavMeshPath path = new NavMeshPath();
                                if(NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path)) {
                                    List<string> corners = new List<string>();
                                    foreach(var c in path.corners) corners.Add($"\"{c.x},{c.y},{c.z}\"");
                                    localResp = "{\"status\":\"success\", \"pathStatus\":\"" + path.status + "\", \"corners\":[" + string.Join(",", corners) + "]}";
                                } else {
                                    localResp = "{\"error\":\"Failed to calculate path. Is NavMesh baked?\"}";
                                    statusCode = 500;
                                }
                            }
                            else if (url == "/object/tag")
                            {
                                var json = JsonUtility.FromJson<TagRequest>(body);
                                var obj = GameObject.Find(json.name);
                                if(obj!=null) {
#if UNITY_EDITOR
                                    Undo.RecordObject(obj, "Set Tag");
#endif
                                    obj.tag = json.tag;
                                    localResp = "{\"status\":\"tagged\", \"tag\":\"" + json.tag + "\"}";
                                } else { localResp = "{\"error\":\"Object not found\"}"; statusCode=404; }
                            }
                             else if (url == "/components")
                            {
                                var json = JsonUtility.FromJson<InspectRequest>(body);
                                var obj = GameObject.Find(json.name);
                                if (obj != null) {
                                    var comps = obj.GetComponents<Component>();
                                    var list = new List<string>();
                                    foreach(var c in comps) if(c!=null) list.Add(c.GetType().Name);
                                    localResp = "{\"components\":[\"" + string.Join("\",\"", list) + "\"]}";
                                } else { localResp = "{\"error\":\"Object not found\"}"; statusCode = 404; }
                            }
                             else if (url == "/component_add")
                            {
                                var json = JsonUtility.FromJson<InspectRequest>(body); // reusing fields: name=objName, componentName=compType
                                var obj = GameObject.Find(json.name);
                                if (obj != null) {
                                    Type t = Type.GetType(json.componentName);
                                    if(t == null) t = Type.GetType("UnityEngine." + json.componentName + ", UnityEngine");
                                    if(t == null) {
                                        foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                                            t = asm.GetType(json.componentName);
                                            if(t != null) break;
                                        }
                                    }
                                    if(t != null) {
#if UNITY_EDITOR
                                        Undo.AddComponent(obj, t);
#else
                                        obj.AddComponent(t);
#endif
                                        localResp = "{\"status\":\"added\", \"component\":\"" + t.Name + "\"}";
                                    } else {
                                        localResp = "{\"error\":\"Type not found: "+json.componentName+"\"}"; statusCode = 400; 
                                    }
                                } else { localResp = "{\"error\":\"Object not found\"}"; statusCode = 404; }
                            }
                            else if (url == "/component_data")
                            {
                                var json = JsonUtility.FromJson<InspectRequest>(body);
                                var obj = GameObject.Find(json.gameObjectName);
                                if (obj != null) {
                                    var comp = obj.GetComponent(json.componentName);
                                    if(comp != null) {
                                        localResp = GetComponentDataJson(comp);
                                    } else { localResp = "{\"error\":\"Component not found\"}"; statusCode = 404; }
                                } else { localResp = "{\"error\":\"Object not found\"}"; statusCode = 404; }
                            }
                            else if (url == "/component_set")
                            {
                                var json = JsonUtility.FromJson<SetFieldRequest>(body);
                                var obj = GameObject.Find(json.gameObjectName);
                                if (obj != null) {
                                    var comp = obj.GetComponent(json.componentName);
                                    if(comp != null) {
#if UNITY_EDITOR
                                        Undo.RecordObject(comp, "Set " + json.fieldName);
#endif
                                        if(SetComponentField(comp, json.fieldName, json.value))
                                            localResp = "{\"status\":\"ok\"}";
                                        else 
                                            { localResp = "{\"error\":\"Field not found or incompatible type\"}"; statusCode = 400; }
                                    } else { localResp = "{\"error\":\"Component not found\"}"; statusCode = 404; }
                                } else { localResp = "{\"error\":\"Object not found\"}"; statusCode = 404; }
                            }
                        }
                        catch(Exception ex) {
                            localResp = "{\"error\":\"" + ex.Message + "\"}";
                            statusCode = 500;
                            Debug.LogError(ex);
                        }
                        waitHandle.Set();
                    });
                    waitHandle.WaitOne();
                    responseString = localResp;
                }
            }
        }
        catch (Exception ex)
        {
            responseString = "{\"error\": \"" + ex.Message + "\"}";
            statusCode = 500;
        }

        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        try { response.OutputStream.Write(buffer, 0, buffer.Length); response.OutputStream.Close(); } catch {}
    }

    private string GetComponentDataJson(Component comp)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        var type = comp.GetType();
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        bool first = true;
        foreach(var f in fields) {
            if(!first) sb.Append(","); 
            sb.Append($"\"{f.Name}\": \"{f.GetValue(comp)}\"");
            first = false;
        }
        foreach(var p in props) {
            if(!p.CanRead) continue;
            try {
                if(!first) sb.Append(","); 
                sb.Append($"\"{p.Name}\": \"{p.GetValue(comp, null)}\"");
                first = false;
            } catch {}
        }
        sb.Append("}");
        return sb.ToString(); 
    }

    private bool SetComponentField(Component comp, string name, string value)
    {
        var type = comp.GetType();
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        
        object parsedVal = null;
        Type targetType = field != null ? field.FieldType : prop != null ? prop.PropertyType : null;

        if (targetType == null) return false;

        try {
            if (targetType == typeof(float)) parsedVal = float.Parse(value);
            else if (targetType == typeof(int)) parsedVal = int.Parse(value);
            else if (targetType == typeof(string)) parsedVal = value;
            else if (targetType == typeof(bool)) parsedVal = bool.Parse(value);
             else if (targetType == typeof(Vector3)) {
                // simple vector3 parsing "x,y,z"
                var parts = value.Split(',');
                if(parts.Length == 3) {
                     parsedVal = new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
                }
            }
        } catch { return false; }

        if(field != null) field.SetValue(comp, parsedVal);
        else if (prop != null && prop.CanWrite) prop.SetValue(comp, parsedVal, null);
        else return false;

        return true;
    }

    private string GetHierarchy()
    {
        string json = "[]";
        var waitHandle = new AutoResetEvent(false);
        EnqueueMain(() => {
            try {
                var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                var list = new List<string>();
                foreach (var r in roots) list.Add(r.name);
                json = "[" + string.Join(",", list.ConvertAll(s => "\"" + s + "\"")) + "]";
            } catch {}
            waitHandle.Set();
        });
        waitHandle.WaitOne();
        return json;
    }

    private void EnqueueMain(Action action) { lock (mainThreadActions) { mainThreadActions.Enqueue(action); } }
    
    [Serializable] public class PrimitiveRequest { public string type; public string name; }
    [Serializable] public class TransformRequest { public string name; public float x; public float y; public float z; }
    [Serializable] public class InspectRequest { public string name; public string gameObjectName; public string componentName; } 
    [Serializable] public class SetFieldRequest { public string gameObjectName; public string componentName; public string fieldName; public string value; }
    // Logging Infrastructure
    private static List<string> logBuffer = new List<string>();
    private static object logLock = new object();
    private const int MAX_LOGS = 50;



    private void HandleLog(string logString, string stackTrace, LogType type) {
        lock(logLock) {
            if(logBuffer.Count >= MAX_LOGS) logBuffer.RemoveAt(0);
            string prefix = type == LogType.Error || type == LogType.Exception ? "[ERROR] " : "[LOG] ";
            logBuffer.Add(prefix + logString);
        }
    }

    [Serializable] public class ScriptRequest { public string fileName; public string code; }

    [Serializable] public class TagRequest { public string tag; public string name; }
    [Serializable] public class ParentRequest { public string name; public string parentName; }
    [Serializable] public class InstantiateRequest { public string path; public string name; }
    [Serializable] public class InvokeRequest { public string gameObjectName; public string componentName; public string methodName; public string parameter; }
    
    [Serializable] public class PhysicsRequest { 
        public string type; // "ray" or "sphere"
        public string origin; // GameObject name
        public bool useVectorOrigin; public float originX; public float originY; public float originZ;
        public float dirX; public float dirY; public float dirZ; // for ray
        public float maxDistance; 
        public float radius; // for sphere
    }

    [Serializable] public class NavMeshRequest {
        public string from; public string to; // GameObject names
        public bool useVectors;
        public float startX; public float startY; public float startZ;
        public float endX; public float endY; public float endZ;
    }
}
