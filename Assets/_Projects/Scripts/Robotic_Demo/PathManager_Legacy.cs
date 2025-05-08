using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PathManager_Legacy : MonoBehaviour
{
    public GameObject pathPointPrefab;
    public GameObject targetObject;
    public GameObject moverObject;
    public float speed = 5f;
    [Range(0.05f, 1f)]
    public float density = 0.1f;
    public Button generatePathButton, startButton;
    public Transform pathPointsContainer;
    private Queue<GameObject> pathPointPool = new Queue<GameObject>();
    private Vector3 InitialPosition_Mover;

    public List<Vector3> orderedPath;
    public int currentTargetIndex = 0;
    public bool CanMove = false;

    private LineRenderer pathLineRenderer;
    public bool showPath = true;
    public float lineWidth = 0.05f;
    public Color passedColor = Color.red;
    public Color unpassedColor = Color.green;
    public Color pulseColor = Color.white;

    // Pulsing effect variables
    private bool isPulsing = false;
    public float pulseSpeed = 2f;
    public float pulseWidth = 0.3f;
    private float pulseTime = 0f;

    // Offset variable
    public float offsetFromSurface = 0.1f; // Distance to offset points from the surface along the normal

    private void Start()
    {
        InitialPosition_Mover = moverObject.transform.position;

        pathLineRenderer = gameObject.AddComponent<LineRenderer>();
        pathLineRenderer.positionCount = 0;
        pathLineRenderer.startWidth = lineWidth;
        pathLineRenderer.endWidth = lineWidth;
        pathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        pathLineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor);
    }

    private void Update()
    {
        if (CanMove)
        {
            if (orderedPath.Count > 0 && currentTargetIndex < orderedPath.Count)
            {
                Vector3 targetPosition = orderedPath[currentTargetIndex];
                moverObject.transform.position = Vector3.MoveTowards(moverObject.transform.position, targetPosition, speed * Time.deltaTime);

                if (Vector3.Distance(moverObject.transform.position, targetPosition) < 0.1f)
                {
                    currentTargetIndex++;
                    UpdateLineColors();
                }

                //UpdateLineColors();
            }
            else
            {
                CanMove = false;
                Vector3 startPosition = InitialPosition_Mover;
                float travelTime = Vector3.Distance(moverObject.transform.position, startPosition) / speed;
                MoveToStartPositionTween = moverObject.transform.DOMove(startPosition, travelTime, false).SetEase(Ease.Linear);
            }
        }

        if (isPulsing && showPath && orderedPath.Count > 1)
        {
            pulseTime += Time.deltaTime * pulseSpeed;
            UpdatePulseEffect();
        }
    }

    public void StartMoving()
    {
        CanMove = false;
        MoveToStartPositionTween.Kill();
        MoveToStartPositionTween = null;
        currentTargetIndex = 0;
        Vector3 startPosition = orderedPath.Count > 0 ? orderedPath[0] : InitialPosition_Mover;
        float travelTime = Vector3.Distance(moverObject.transform.position, startPosition) / speed;

        isPulsing = false;
        if (showPath && orderedPath.Count > 1)
        {
            pathLineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor);
        }

        moverObject.transform.DOMove(startPosition, travelTime, false).SetEase(Ease.Linear).OnComplete(() =>
        {
            CanMove = true;
        });
    }

    private Tween MoveToStartPositionTween;

    public void GeneratePathPoints()
    {
        CanMove = false;
        Vector3 startPosition = InitialPosition_Mover;
        float travelTime = Vector3.Distance(moverObject.transform.position, startPosition) / speed;
        MoveToStartPositionTween = moverObject.transform.DOMove(startPosition, travelTime, false).SetEase(Ease.Linear);

        // Clear existing path points
        foreach (Transform child in pathPointsContainer)
        {
            pathPointPool.Enqueue(child.gameObject);
            child.gameObject.SetActive(false);
        }

        pathLineRenderer.positionCount = 0;

        // Get mesh data
        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        // Convert to world space
        List<Vector3> worldVertices = vertices.Select(v => targetObject.transform.TransformPoint(v)).ToList();
        List<Vector3> worldNormals = new List<Vector3>();
        for (int i = 0; i < normals.Length; i++)
        {
            Vector3 worldNormal = targetObject.transform.TransformDirection(normals[i]).normalized;
            worldNormals.Add(worldNormal);
        }

        if (worldVertices.Count == 0)
        {
            Debug.LogWarning("No vertices found in the mesh to generate path points.");
            return;
        }

        // Calculate bounding box
        Vector3 minBounds = new Vector3(
            worldVertices.Min(p => p.x),
            worldVertices.Min(p => p.y),
            worldVertices.Min(p => p.z)
        );
        Vector3 maxBounds = new Vector3(
            worldVertices.Max(p => p.x),
            worldVertices.Max(p => p.y),
            worldVertices.Max(p => p.z)
        );
        Vector3 boundsRange = maxBounds - minBounds;

        // Determine if the surface is 2D-like (small range in one axis)
        bool is2D = boundsRange.x <= 0.1f || boundsRange.y <= 0.1f || boundsRange.z <= 0.1f;

        int totalVertices = worldVertices.Count;
        int desiredPointCount = Mathf.Max(2, Mathf.FloorToInt(totalVertices * density));

        List<Vector3> sampledPoints = new List<Vector3>();
        List<Vector3> sampledNormals = new List<Vector3>();

        if (is2D)
        {
            // Use original 2D grid approach for flat surfaces
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
            int gridWidth = gridSize;
            int gridHeight = gridSize;

            float rangeAxis1 = axis1 == "X" ? boundsRange.x : axis1 == "Y" ? boundsRange.y : boundsRange.z;
            float rangeAxis2 = axis2 == "X" ? boundsRange.x : axis2 == "Y" ? boundsRange.y : boundsRange.z;
            float cellWidth = rangeAxis1 / gridWidth;
            float cellHeight = rangeAxis2 / gridHeight;

            List<Vector3>[,] grid = new List<Vector3>[gridWidth, gridHeight];
            List<Vector3>[,] normalGrid = new List<Vector3>[gridWidth, gridHeight];
            for (int i = 0; i < gridWidth; i++)
            {
                for (int j = 0; j < gridHeight; j++)
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

                gridX = Mathf.Clamp(gridX, 0, gridWidth - 1);
                gridY = Mathf.Clamp(gridY, 0, gridHeight - 1);

                grid[gridX, gridY].Add(vertex);
                normalGrid[gridX, gridY].Add(normal);
            }

            for (int i = 0; i < gridWidth; i++)
            {
                for (int j = 0; j < gridHeight; j++)
                {
                    if (grid[i, j].Count > 0)
                    {
                        Vector3 averagePosition = Vector3.zero;
                        Vector3 averageNormal = Vector3.zero;
                        foreach (Vector3 vertex in grid[i, j])
                        {
                            averagePosition += vertex;
                        }
                        foreach (Vector3 normal in normalGrid[i, j])
                        {
                            averageNormal += normal;
                        }
                        averagePosition /= grid[i, j].Count;
                        averageNormal = (averageNormal / normalGrid[i, j].Count).normalized;
                        sampledPoints.Add(averagePosition);
                        sampledNormals.Add(averageNormal);
                    }
                }
            }
        }
        else
        {
            // Use 3D voxel grid for complex 3D surfaces
            Debug.Log("Using 3D voxel grid for sampling");

            int voxelSize = Mathf.CeilToInt(Mathf.Pow(desiredPointCount, 1f / 3f)); // Approximate cube root
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

            // Assign vertices to 3D voxels
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

            // Sample points from non-empty voxels
            for (int i = 0; i < voxelSize; i++)
            {
                for (int j = 0; j < voxelSize; j++)
                {
                    for (int k = 0; k < voxelSize; k++)
                    {
                        if (voxelGrid[i, j, k].Count > 0)
                        {
                            Vector3 averagePosition = Vector3.zero;
                            Vector3 averageNormal = Vector3.zero;
                            foreach (Vector3 vertex in voxelGrid[i, j, k])
                            {
                                averagePosition += vertex;
                            }
                            foreach (Vector3 normal in normalVoxelGrid[i, j, k])
                            {
                                averageNormal += normal;
                            }
                            averagePosition /= voxelGrid[i, j, k].Count;
                            averageNormal = (averageNormal / voxelGrid[i, j, k].Count).normalized;
                            sampledPoints.Add(averagePosition);
                            sampledNormals.Add(averageNormal);
                        }
                    }
                }
            }
        }

        // Ensure at least two points
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

        // Apply offset and instantiate path points
        for (int i = 0; i < sampledPoints.Count; i++)
        {
            Vector3 pointPosition = sampledPoints[i];
            Vector3 normal = sampledNormals[i];
            pointPosition += normal * offsetFromSurface;

            GameObject pathPoint = GetPathPointFromPool();
            pathPoint.transform.position = pointPosition;
            pathPoint.SetActive(true);

            sampledPoints[i] = pointPosition;
        }

        // Sort points (replace with a 3D-friendly approach)
        SortPoints3D(sampledPoints);

        isPulsing = true;
        pulseTime = 0f;
    }

    private void SortPoints3D(List<Vector3> pathPoints)
    {
        orderedPath.Clear();
        if (pathPoints.Count == 0) return;

        // Start with the first point
        List<Vector3> remainingPoints = new List<Vector3>(pathPoints);
        Vector3 currentPoint = remainingPoints[0];
        orderedPath.Add(currentPoint);
        remainingPoints.RemoveAt(0);

        // Connect to the nearest unvisited point
        while (remainingPoints.Count > 0)
        {
            int nearestIndex = 0;
            float minDistance = float.MaxValue;

            for (int i = 0; i < remainingPoints.Count; i++)
            {
                float distance = Vector3.Distance(currentPoint, remainingPoints[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            currentPoint = remainingPoints[nearestIndex];
            orderedPath.Add(currentPoint);
            remainingPoints.RemoveAt(nearestIndex);
        }

        // Update LineRenderer
        if (showPath && orderedPath.Count > 1)
        {
            pathLineRenderer.positionCount = orderedPath.Count;
            pathLineRenderer.SetPositions(orderedPath.ToArray());
            pathLineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor);
        }
        else
        {
            pathLineRenderer.positionCount = 0;
        }
    }

    GameObject GetPathPointFromPool()
    {
        return pathPointPool.Count > 0 ? pathPointPool.Dequeue() : Instantiate(pathPointPrefab, pathPointsContainer);
    }

    private Gradient CreateGradient(Color startColor, Color endColor)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }

    private void UpdateLineColors()
    {
        if (!showPath || orderedPath.Count < 2) return;

        Gradient gradient = new Gradient();
        List<GradientColorKey> colorKeys = new List<GradientColorKey>();
        List<GradientAlphaKey> alphaKeys = new List<GradientAlphaKey>();

        float fractionPerPoint = 1f / (orderedPath.Count - 1);
        float currentFraction = currentTargetIndex * fractionPerPoint;

        colorKeys.Add(new GradientColorKey(passedColor, 0f));
        colorKeys.Add(new GradientColorKey(passedColor, currentFraction));
        colorKeys.Add(new GradientColorKey(unpassedColor, currentFraction));
        colorKeys.Add(new GradientColorKey(unpassedColor, 1f));

        alphaKeys.Add(new GradientAlphaKey(1f, 0f));
        alphaKeys.Add(new GradientAlphaKey(1f, 1f));

        gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
        pathLineRenderer.colorGradient = gradient;
    }

    private void UpdatePulseEffect()
    {
        Gradient gradient = new Gradient();
        List<GradientColorKey> colorKeys = new List<GradientColorKey>();
        List<GradientAlphaKey> alphaKeys = new List<GradientAlphaKey>();

        float pulsePosition = (pulseTime % 1f);
        float pulseStart = pulsePosition - pulseWidth / 2f;
        float pulseEnd = pulsePosition + pulseWidth / 2f;

        pulseStart = Mathf.Clamp(pulseStart, 0f, 1f);
        pulseEnd = Mathf.Clamp(pulseEnd, 0f, 1f);

        if (pulseStart > 0f)
        {
            colorKeys.Add(new GradientColorKey(unpassedColor, 0f));
            colorKeys.Add(new GradientColorKey(unpassedColor, pulseStart));
        }

        if (pulseStart < pulseEnd)
        {
            colorKeys.Add(new GradientColorKey(pulseColor, pulseStart));
            colorKeys.Add(new GradientColorKey(pulseColor, pulseEnd));
        }

        if (pulseEnd < 1f)
        {
            colorKeys.Add(new GradientColorKey(unpassedColor, pulseEnd));
            colorKeys.Add(new GradientColorKey(unpassedColor, 1f));
        }

        alphaKeys.Add(new GradientAlphaKey(1f, 0f));
        alphaKeys.Add(new GradientAlphaKey(1f, 1f));

        gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
        pathLineRenderer.colorGradient = gradient;
    }

    #region Path Sorting
    public void SortPoints(List<Vector3> pathPoints)
    {
        orderedPath.Clear();

        Vector3 minBounds = new Vector3(
            pathPoints.Min(p => p.x),
            pathPoints.Min(p => p.y),
            pathPoints.Min(p => p.z)
        );

        Vector3 maxBounds = new Vector3(
            pathPoints.Max(p => p.x),
            pathPoints.Max(p => p.y),
            pathPoints.Max(p => p.z)
        );

        Vector3 boundsRange = maxBounds - minBounds;

        string dominantAxis = (boundsRange.x > boundsRange.y && boundsRange.x > boundsRange.z) ? "X" :
                             (boundsRange.y > boundsRange.x && boundsRange.y > boundsRange.z) ? "Y" : "Z";

        Debug.Log("DominantAxis: " + dominantAxis);

        float maxRange = Mathf.Max(boundsRange.x, boundsRange.y, boundsRange.z);

        var sortedDominantValues = pathPoints
            .Select(p => GetAxisValue(p, dominantAxis))
            .OrderBy(v => v)
            .ToList();

        float totalSpacing = 0f;
        for (int i = 1; i < sortedDominantValues.Count; i++)
        {
            totalSpacing += Mathf.Abs(sortedDominantValues[i] - sortedDominantValues[i - 1]);
        }
        float avgSpacing = totalSpacing / Mathf.Max(1, sortedDominantValues.Count - 1);

        float dynamicThreshold = avgSpacing * 1.5f;

        Debug.Log(dynamicThreshold);

        List<List<Vector3>> rows = new List<List<Vector3>>();
        List<Vector3> currentRow = new List<Vector3>();
        float lastAxisValue = GetAxisValue(pathPoints[0], dominantAxis);

        foreach (Vector3 point in pathPoints.OrderBy(p => GetAxisValue(p, dominantAxis)))
        {
            if (Mathf.Abs(GetAxisValue(point, dominantAxis) - lastAxisValue) > dynamicThreshold)
            {
                rows.Add(new List<Vector3>(currentRow));
                currentRow.Clear();
            }

            currentRow.Add(point);
            lastAxisValue = GetAxisValue(point, dominantAxis);
        }
        if (currentRow.Count > 0) rows.Add(currentRow);

        for (int i = 0; i < rows.Count; i++)
        {
            string secondaryAxis = dominantAxis == "X" ? (boundsRange.y > boundsRange.z ? "Y" : "Z") :
                                  dominantAxis == "Y" ? (boundsRange.x > boundsRange.z ? "X" : "Z") :
                                  (boundsRange.x > boundsRange.y ? "X" : "Y");

            var sortedRow = rows[i].OrderBy(p => GetAxisValue(p, secondaryAxis)).ToList();

            if (i % 2 == 1)
            {
                sortedRow.Reverse();
            }

            orderedPath.AddRange(sortedRow);
        }

        if (showPath && orderedPath.Count > 1)
        {
            pathLineRenderer.positionCount = orderedPath.Count;
            pathLineRenderer.SetPositions(orderedPath.ToArray());
            pathLineRenderer.colorGradient = CreateGradient(unpassedColor, unpassedColor);
        }
        else
        {
            pathLineRenderer.positionCount = 0;
        }
    }

    private float GetAxisValue(Vector3 point, string axis)
    {
        return axis == "X" ? point.x : axis == "Y" ? point.y : point.z;
    }
    #endregion
}