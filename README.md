# Unity MCP Server 🎮🤖

**Control Unity with AI.**

This project implements a Model Context Protocol (MCP) server that acts as a bridge between AI agents (like Claude Desktop, Cursor, or custom LLM clients) and the Unity Engine.

It allows an AI to **see**, **create**, **modify**, and **control** everything in your Unity scene in real-time.

## 🌟 Features

*   **God Mode Control**: Create primitives, instantiate prefabs, set parents, rotate, scale, and move objects.
*   **Editor & Play Mode**: Works in both! Modify the scene while the game is running or while editing level design.
*   **Full Reflection**: Inspect components, read fields, and modify values (int, float, bool, string, Vector3) dynamically.
*   **Logic Execution**: Invoke any public method on any component.
*   **Tag Management**: Create and assign tags on the fly.
*   **Cleanup Tools**: Delete objects to reset scenes.

## 🛠️ Installation

### 1. Unity Setup (`/Assets`)
1.  Copy the `Assets/Scripts/MCP` folder into your Unity project.
2.  Add the `MCPBridge` script to any GameObject in your scene (or create an empty one named "MCPBridge").
3.  The server runs on `http://localhost:8080`.

### 2. MCP Server Setup (`/mcp-server`)
1.  Install Node.js.
2.  Navigate to the `mcp-server` folder:
    ```bash
    cd mcp-server
    ```
3.  Install dependencies:
    ```bash
    npm install
    ```
4.  Build and run:
    ```bash
    npm run build
    npm start
    ```

### 3. Client Configuration
Add the server to your MCP Client config (e.g., `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "unity": {
      "command": "node",
      "args": ["/path/to/mcp-server/dist/index.js"]
    }
  }
}
```

## 📚 Available Tools

| Tool | Description |
| :--- | :--- |
| `create_primitive` | Spawn basic shapes (Cube, Sphere, etc.) |
| `instantiate_prefab` | Spawn complex assets from Resources |
| `set_object_transform` | Move objects (Position) |
| `set_object_rotation` | Rotate objects (Euler Angles) |
| `set_object_scale` | Resize objects |
| `set_parent` | Organize hierarchy |
| `inspect_component` | Read public variables of a script |
| `edit_component` | Change variable values (speed, health, etc.) |
| `invoke_method` | Call functions (Jump(), Attack(), Reset()) |
| `add_tag` / `set_object_tag` | Manage tags |
| `delete_object` | Remove objects from the scene |

## 📝 Example Prompts

*   *"Create a red cube and place it at (0, 5, 0)."*
*   *"Build a tower of 10 spheres stacked on top of each other."*
*   *"Inspect the PlayerController on the 'Hero' object and set speed to 20."*
*   *"Make the 'Main Camera' look at the 'Hero'."*

## ⚠️ Requirements
*   Unity 2020.3 or later (tested on 6000.0.26f1).
*   Node.js 16+.


