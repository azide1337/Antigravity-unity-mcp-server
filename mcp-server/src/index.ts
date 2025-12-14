import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import axios from "axios";

const UNITY_API_URL = "http://localhost:8080";

// Create server instance
const server = new McpServer({
    name: "UnityMCP",
    version: "1.0.0",
});

async function callUnity(endpoint: string, method: 'GET' | 'POST', data?: any) {
    try {
        const response = await axios({
            method,
            url: `${UNITY_API_URL}${endpoint}`,
            data,
        });
        return response.data;
    } catch (error: any) {
        if (error.code === 'ECONNREFUSED') {
            return { error: "Unity is not running or MCPBridge is not active." };
        }
        return { error: error.message };
    }
}

server.tool("get_hierarchy", "List root GameObjects.", {}, async () => {
    const result = await callUnity("/hierarchy", "GET");
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
});

server.tool("create_primitive", "Create primitive with optional name.", {
    type: z.enum(["Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad"]),
    name: z.string().optional().describe("Custom name. Auto-increments if duplicate."),
}, async ({ type, name }) => {
    const result = await callUnity("/create_primitive", "POST", { type, name });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Created: ${result.name}` }] };
});

server.tool("set_object_transform", "Set position.", {
    name: z.string(), x: z.number(), y: z.number(), z: z.number(),
}, async ({ name, x, y, z }) => {
    const result = await callUnity("/transform", "POST", { name, x, y, z });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Moved ${result.name}` }] };
});

server.tool("set_object_scale", "Set scale.", {
    name: z.string(), x: z.number(), y: z.number(), z: z.number(),
}, async ({ name, x, y, z }) => {
    const result = await callUnity("/scale", "POST", { name, x, y, z });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Scaled ${result.name}` }] };
});

server.tool("get_object_components", "Get list of components.", {
    name: z.string(),
}, async ({ name }) => {
    const result = await callUnity("/components", "POST", { name });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(result.components, null, 2) }] };
});

server.tool("inspect_component", "Get public fields.", {
    objectName: z.string(),
    componentName: z.string(),
}, async ({ objectName, componentName }) => {
    const result = await callUnity("/component_data", "POST", { gameObjectName: objectName, componentName });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
});

server.tool("edit_component", "Set a field.", {
    objectName: z.string(),
    componentName: z.string(),
    fieldName: z.string(),
    value: z.string(),
}, async ({ objectName, componentName, fieldName, value }) => {
    const result = await callUnity("/component_set", "POST", { gameObjectName: objectName, componentName, fieldName, value });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Updated ${fieldName} to ${value}` }] };
});

server.tool("add_component", "Add a component to an object.", {
    objectName: z.string(),
    componentName: z.string(),
}, async ({ objectName, componentName }) => {
    const result = await callUnity("/component_add", "POST", { name: objectName, componentName });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Added ${result.component} to ${objectName}` }] };
});

server.tool("add_tag", "Create a new Tag in the Project Settings (Editor Only).", {
    tag: z.string(),
}, async ({ tag }) => {
    const result = await callUnity("/tag_add", "POST", { tag });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Tag created: ${tag}` }] };
});

server.tool("set_object_tag", "Assign a tag to an object.", {
    name: z.string(),
    tag: z.string(),
}, async ({ name, tag }) => {
    const result = await callUnity("/object/tag", "POST", { name, tag });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Set tag ${tag} on ${name}` }] };
});

server.tool("delete_object", "Delete an object.", {
    name: z.string(),
}, async ({ name }) => {
    const result = await callUnity("/object_delete", "POST", { name });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Deleted: ${name}` }] };
});

server.tool("set_object_rotation", "Set rotation (Euler angles).", {
    name: z.string(), x: z.number(), y: z.number(), z: z.number(),
}, async ({ name, x, y, z }) => {
    const result = await callUnity("/rotation", "POST", { name, x, y, z });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Rotated ${result.name}` }] };
});

server.tool("set_parent", "Set object parent.", {
    name: z.string(),
    parentName: z.string(),
}, async ({ name, parentName }) => {
    const result = await callUnity("/parent", "POST", { name, parentName });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Parented ${name} to ${parentName}` }] };
});

server.tool("instantiate_prefab", "Instantiate a prefab.", {
    path: z.string().describe("Path to prefab (e.g. Assets/Prefabs/Player.prefab or Resources path)"),
    name: z.string().optional(),
}, async ({ path, name }) => {
    const result = await callUnity("/instantiate", "POST", { path, name });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Created ${result.name}` }] };
});

server.tool("invoke_method", "Invoke a method on a component.", {
    objectName: z.string(),
    componentName: z.string(),
    methodName: z.string(),
    parameter: z.string().optional().describe("Optional string/int/float parameter"),
}, async ({ objectName, componentName, methodName, parameter }) => {
    const result = await callUnity("/invoke", "POST", { gameObjectName: objectName, componentName, methodName, parameter });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Invoked ${methodName}` }] };
});



server.tool("get_screenshot", "Capture a screenshot of the Game View.", {}, async () => {
    const result = await callUnity("/screenshot", "POST");
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return {
        content: [
            { type: "text", text: "Screenshot captured." },
            { type: "image", data: result.image, mimeType: "image/jpeg" }
        ]
    };
});

server.tool("get_console_logs", "Get recent Unity console logs.", {}, async () => {
    const result = await callUnity("/logs", "GET");
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: result.logs || "No new logs." }] };
});

server.tool("create_script", "Create a new C# script.", {
    fileName: z.string().describe("Filename including .cs extension (e.g. MyScript.cs)"),
    code: z.string().describe("The full C# code"),
}, async ({ fileName, code }) => {
    const result = await callUnity("/create_script", "POST", { fileName, code });
    if (result.error) return { content: [{ type: "text", text: `Error: ${result.error}` }] };
    return { content: [{ type: "text", text: `Created script at ${result.path}. Unity is compiling...` }] };
});

async function main() {
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error("Unity MCP Server running on stdio");
}

main();
