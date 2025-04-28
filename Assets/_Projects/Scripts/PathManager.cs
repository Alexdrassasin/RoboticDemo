using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

public class PathManager : MonoBehaviour
{
    public GameObject pathPointPrefab;
    public GameObject targetObject;
    public GameObject moverObject;
    public float speed = 5f;
    public float density = 0.1f;
    public Button generatePathButton, startButton;
    public Transform pathPointsContainer;
    private Queue<GameObject> pathPointPool = new Queue<GameObject>();
    private Vector3 InitialPosition_Mover;

    public List<Vector3> orderedPath;
    public int currentTargetIndex = 0;

    public bool CanMove = false;
    private void Start()
    {
        InitialPosition_Mover = moverObject.transform.position;
    }

    private void Update()
    {
        if (CanMove)
        {
            if (orderedPath.Count > 0 && currentTargetIndex < orderedPath.Count)
            {
                Vector3 targetPosition = orderedPath[currentTargetIndex];
                Debug.Log(targetPosition);
                moverObject.transform.position = Vector3.MoveTowards(moverObject.transform.position, targetPosition, speed * Time.deltaTime);

                if (Vector3.Distance(moverObject.transform.position, targetPosition) < 0.1f)
                {
                    currentTargetIndex++; // Move to the next point
                }
            }
        }

    }

    public void StartMoving()
    {
        CanMove = false;
        currentTargetIndex = 0;
        moverObject.transform.DOMove(InitialPosition_Mover, 0.5f, true).OnComplete(() =>
        {
            CanMove = true;
        });
    }

    public void GeneratePathPoints()
    {
        // Disable and return old path points to the pool
        foreach (Transform child in pathPointsContainer)
        {
            pathPointPool.Enqueue(child.gameObject);
            child.gameObject.SetActive(false);
        }

        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] worldVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            worldVertices[i] = targetObject.transform.TransformPoint(vertices[i]);
        }

        // Generate new path points
        for (int i = 0; i < density; i++)
        {
            int index = Mathf.FloorToInt(i * (vertices.Length / (float)density));
            Vector3 pointPosition = worldVertices[index];

            GameObject pathPoint = GetPathPointFromPool();
            pathPoint.transform.position = pointPosition;
            pathPoint.transform.rotation = Quaternion.identity;
            pathPoint.SetActive(true);  // Enable the point
        }

        SortPoints();
    }

    GameObject GetPathPointFromPool()
    {
        if (pathPointPool.Count > 0)
        {
            return pathPointPool.Dequeue();
        }
        else
        {
            // If the pool is empty, instantiate a new point
            return Instantiate(pathPointPrefab, pathPointsContainer);
        }
    }

    #region Path Sorting
    public void SortPoints()
    {
        orderedPath.Clear();
        List<Vector3> pathPoints = new List<Vector3>();

        // Collect all path points
        foreach (Transform child in pathPointsContainer)
        {
            pathPoints.Add(child.position);
        }

        // Sort all points by Z (depth order) to organize them into rows
        pathPoints = pathPoints.OrderBy(p => p.z).ToList();

        List<List<Vector3>> rows = new List<List<Vector3>>();
        float threshold = 0.1f;  // Adjust based on mesh density

        List<Vector3> currentRow = new List<Vector3>();
        float lastZ = pathPoints[0].z;

        foreach (Vector3 point in pathPoints)
        {
            if (Mathf.Abs(point.z - lastZ) > threshold)
            {
                rows.Add(new List<Vector3>(currentRow));
                currentRow.Clear();
            }

            currentRow.Add(point);
            lastZ = point.z;
        }

        if (currentRow.Count > 0)
        {
            rows.Add(currentRow);
        }

        // Zigzag ordering: Reverse every second row to create U-turns
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i] = rows[i].OrderBy(p => p.x).ToList();  // Sort row points by X-axis

            if (i % 2 == 1)
            {
                rows[i].Reverse();  // Reverse odd-indexed rows for U-turn effect
            }
        }

        // Flatten the list into orderedPath
        foreach (var row in rows)
        {
            orderedPath.AddRange(row);
        }
    }

    #endregion
}
