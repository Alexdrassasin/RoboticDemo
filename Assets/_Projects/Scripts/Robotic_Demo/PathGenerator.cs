using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PathGenerator
{
    private readonly GameObject targetObject;
    private float density;
    private float offsetFromSurface;
    private readonly PathPointPool pointPool;
    private readonly Transform pathPointsContainer;

    public PathGenerator(GameObject targetObject, float density, float offsetFromSurface, PathPointPool pointPool, Transform pathPointsContainer)
    {
        this.targetObject = targetObject;
        this.density = density;
        this.offsetFromSurface = offsetFromSurface;
        this.pointPool = pointPool;
        this.pathPointsContainer = pathPointsContainer;
    }

    public List<Vector3> GeneratePathPoints()
    {
        pointPool.Clear(pathPointsContainer);

        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        List<Vector3> worldVertices = vertices.Select(v => targetObject.transform.TransformPoint(v)).ToList();
        List<Vector3> worldNormals = normals.Select(n => targetObject.transform.TransformDirection(n).normalized).ToList();

        if (worldVertices.Count == 0)
        {
            Debug.LogWarning("No vertices found in the mesh to generate path points.");
            return new List<Vector3>();
        }

        Vector3 minBounds = new Vector3(worldVertices.Min(p => p.x), worldVertices.Min(p => p.y), worldVertices.Min(p => p.z));
        Vector3 maxBounds = new Vector3(worldVertices.Max(p => p.x), worldVertices.Max(p => p.y), worldVertices.Max(p => p.z));
        Vector3 boundsRange = maxBounds - minBounds;

        bool is2D = boundsRange.x <= 0.1f || boundsRange.y <= 0.1f || boundsRange.z <= 0.1f;
        int totalVertices = worldVertices.Count;
        int desiredPointCount = Mathf.Max(2, Mathf.FloorToInt(totalVertices * density));

        List<Vector3> sampledPoints = new List<Vector3>();
        List<Vector3> sampledNormals = new List<Vector3>();

        if (is2D)
        {
            string axis1, axis2, fixedAxis;
            if (boundsRange.x <= boundsRange.y && boundsRange.x <= boundsRange.z)
            {
                axis1 = "Y"; axis2 = "Z"; fixedAxis = "X";
            }
            else if (boundsRange.y <= boundsRange.x && boundsRange.y <= boundsRange.z)
            {
                axis1 = "X"; axis2 = "Z"; fixedAxis = "Y";
            }
            else
            {
                axis1 = "X"; axis2 = "Y"; fixedAxis = "Z";
            }

            Debug.Log($"Using 2D grid on plane: {axis1}-{axis2}, fixed axis: {fixedAxis}");

            int gridSize = Mathf.CeilToInt(Mathf.Sqrt(desiredPointCount));
            float rangeAxis1 = axis1 == "X" ? boundsRange.x : axis1 == "Y" ? boundsRange.y : boundsRange.z;
            float rangeAxis2 = axis2 == "X" ? boundsRange.x : axis2 == "Y" ? boundsRange.y : boundsRange.z;
            float cellWidth = rangeAxis1 / gridSize;
            float cellHeight = rangeAxis2 / gridSize;

            List<Vector3>[,] grid = new List<Vector3>[gridSize, gridSize];
            List<Vector3>[,] normalGrid = new List<Vector3>[gridSize, gridSize];
            for (int i = 0; i < gridSize; i++)
            {
                for (int j = 0; j < gridSize; j++)
                {
                    grid[i, j] = new List<Vector3>();
                    normalGrid[i, j] = new List<Vector3>();
                }
            }

            for (int i = 0; i < worldVertices.Count; i++)
            {
                Vector3 vertex = worldVertices[i];
                Vector3 normal = worldNormals[i];

                float value1 = axis1 == "X" ? vertex.x : axis1 == "Y" ? vertex.y : vertex.z;
                float value2 = axis2 == "X" ? vertex.x : axis2 == "Y" ? vertex.y : vertex.z;

                int gridX = Mathf.FloorToInt((value1 - (axis1 == "X" ? minBounds.x : axis1 == "Y" ? minBounds.y : minBounds.z)) / cellWidth);
                int gridY = Mathf.FloorToInt((value2 - (axis2 == "X" ? minBounds.x : axis2 == "Y" ? minBounds.y : minBounds.z)) / cellHeight);

                gridX = Mathf.Clamp(gridX, 0, gridSize - 1);
                gridY = Mathf.Clamp(gridY, 0, gridSize - 1);

                grid[gridX, gridY].Add(vertex);
                normalGrid[gridX, gridY].Add(normal);
            }

            for (int i = 0; i < gridSize; i++)
            {
                for (int j = 0; j < gridSize; j++)
                {
                    if (grid[i, j].Count > 0)
                    {
                        Vector3 averagePosition = grid[i, j].Aggregate(Vector3.zero, (sum, v) => sum + v) / grid[i, j].Count;
                        Vector3 averageNormal = normalGrid[i, j].Aggregate(Vector3.zero, (sum, n) => sum + n).normalized;
                        sampledPoints.Add(averagePosition);
                        sampledNormals.Add(averageNormal);
                    }
                }
            }
        }
        else
        {
            Debug.Log("Using 3D voxel grid for sampling");

            int voxelSize = Mathf.CeilToInt(Mathf.Pow(desiredPointCount, 1f / 3f));
            float voxelWidth = boundsRange.x / voxelSize;
            float voxelHeight = boundsRange.y / voxelSize;
            float voxelDepth = boundsRange.z / voxelSize;

            List<Vector3>[,,] voxelGrid = new List<Vector3>[voxelSize, voxelSize, voxelSize];
            List<Vector3>[,,] normalVoxelGrid = new List<Vector3>[voxelSize, voxelSize, voxelSize];
            for (int i = 0; i < voxelSize; i++)
            {
                for (int j = 0; j < voxelSize; j++)
                {
                    for (int k = 0; k < voxelSize; k++)
                    {
                        voxelGrid[i, j, k] = new List<Vector3>();
                        normalVoxelGrid[i, j, k] = new List<Vector3>();
                    }
                }
            }

            for (int i = 0; i < worldVertices.Count; i++)
            {
                Vector3 vertex = worldVertices[i];
                Vector3 normal = worldNormals[i];

                int voxelX = Mathf.FloorToInt((vertex.x - minBounds.x) / voxelWidth);
                int voxelY = Mathf.FloorToInt((vertex.y - minBounds.y) / voxelHeight);
                int voxelZ = Mathf.FloorToInt((vertex.z - minBounds.z) / voxelDepth);

                voxelX = Mathf.Clamp(voxelX, 0, voxelSize - 1);
                voxelY = Mathf.Clamp(voxelY, 0, voxelSize - 1);
                voxelZ = Mathf.Clamp(voxelZ, 0, voxelSize - 1);

                voxelGrid[voxelX, voxelY, voxelZ].Add(vertex);
                normalVoxelGrid[voxelX, voxelY, voxelZ].Add(normal);
            }

            for (int i = 0; i < voxelSize; i++)
            {
                for (int j = 0; j < voxelSize; j++)
                {
                    for (int k = 0; k < voxelSize; k++)
                    {
                        if (voxelGrid[i, j, k].Count > 0)
                        {
                            Vector3 averagePosition = voxelGrid[i, j, k].Aggregate(Vector3.zero, (sum, v) => sum + v) / voxelGrid[i, j, k].Count;
                            Vector3 averageNormal = normalVoxelGrid[i, j, k].Aggregate(Vector3.zero, (sum, n) => sum + n).normalized;
                            sampledPoints.Add(averagePosition);
                            sampledNormals.Add(averageNormal);
                        }
                    }
                }
            }
        }

        if (sampledPoints.Count < 2)
        {
            Debug.LogWarning("Too few points sampled. Adding a second point to enable path generation.");
            if (sampledPoints.Count == 1)
            {
                sampledPoints.Add(worldVertices[worldVertices.Count - 1]);
                sampledNormals.Add(worldNormals[worldNormals.Count - 1]);
            }
            else
            {
                sampledPoints.AddRange(worldVertices.Take(2));
                sampledNormals.AddRange(worldNormals.Take(2));
            }
        }

        for (int i = 0; i < sampledPoints.Count; i++)
        {
            Vector3 pointPosition = sampledPoints[i] + sampledNormals[i] * offsetFromSurface;
            GameObject pathPoint = pointPool.Get();
            pathPoint.transform.position = pointPosition;
            sampledPoints[i] = pointPosition;
        }

        return sampledPoints;
    }

    // Updates parameters to reflect real-time changes. Requires regenerating path points to apply.
    public void UpdateParameters(float newDensity, float newOffsetFromSurface)
    {
        this.density = newDensity;
        this.offsetFromSurface = newOffsetFromSurface;
    }
}