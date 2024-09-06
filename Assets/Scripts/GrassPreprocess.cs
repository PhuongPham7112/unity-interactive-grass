using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class GrassPreprocess : EditorWindow
{
    float grassDensityValue = 1.0f;
    float grassHeight = 0.5f;
    float grassWidth = 0.1f;
    float grassStiffness = 0.1f;

    private GameObject selectedObject;
    private GameObject prefabToSpawn;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    [MenuItem("Tools/Vertex Prefab Spawner")]
    public static void ShowWindow()
    {
        GetWindow<GrassPreprocess>("Grass Preprocess Spawner");
    }

    private void OnGUI()
    {
        GUILayout.Label("Spawn Grass at Vertices", EditorStyles.boldLabel);

        selectedObject = EditorGUILayout.ObjectField("Target Object", selectedObject, typeof(GameObject), true) as GameObject;
        prefabToSpawn = EditorGUILayout.ObjectField("Prefab to Spawn", prefabToSpawn, typeof(GameObject), false) as GameObject;
        grassDensityValue = EditorGUILayout.FloatField("Density of grass", grassDensityValue);
        grassHeight = EditorGUILayout.FloatField("Height of grass", grassHeight);
        grassWidth = EditorGUILayout.FloatField("Width of grass", grassWidth);
        grassStiffness = EditorGUILayout.FloatField("Stiffness of grass", grassStiffness);

        if (GUILayout.Button("Spawn Prefabs"))
        {
            SpawnPrefabsAtVertices();
        }

        if (GUILayout.Button("Clear Spawned Prefabs"))
        {
            ClearSpawnedPrefabs();
        }

    }

    private void SpawnPrefabsAtVertices()
    {
        if (selectedObject == null || prefabToSpawn == null)
        {
            Debug.LogError("Please assign both a target object and a prefab to spawn.");
            return;
        }

        ClearSpawnedPrefabs();

        MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("Selected object does not have a valid mesh.");
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        float height = prefabToSpawn.GetComponent<MeshFilter>().sharedMesh.bounds.size.y;
        Debug.Log(height * prefabToSpawn.transform.localScale.y);

        Transform objectTransform = selectedObject.transform;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = objectTransform.TransformPoint(vertices[i]);
            Vector3 worldNormal = objectTransform.TransformDirection(normals[i]);
            float heightOffset = height * prefabToSpawn.transform.localScale.y * 0.5f;

            GameObject spawnedObject = PrefabUtility.InstantiatePrefab(prefabToSpawn) as GameObject;
            spawnedObject.transform.rotation = Quaternion.FromToRotation(spawnedObject.transform.up, worldNormal);
            spawnedObject.transform.position = worldPos + worldNormal * heightOffset;
            spawnedObject.transform.SetParent(selectedObject.transform);
            spawnedObjects.Add(spawnedObject);
        }

        Debug.Log($"Spawned {vertices.Length} prefabs at vertices.");
    }

    private void ClearSpawnedPrefabs()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            DestroyImmediate(obj);
        }
        spawnedObjects.Clear();
    }
}
