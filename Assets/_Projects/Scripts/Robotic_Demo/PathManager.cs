using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PathManager : MonoBehaviour
{
    [Header("Path Objects")]
    [Tooltip("Prefab for path point visualization")]
    public GameObject pathPointPrefab;

    [Tooltip("Target object with mesh for path generation")]
    public GameObject targetObject;

    [Tooltip("Object that moves along the generated path")]
    public GameObject RobotEndPoint;

    [Header("Path Settings")]
    [Tooltip("Speed of the mover object (units per second)")]
    public float EndPointMoveSpeed = 5f;

    [Tooltip("Density of path points (0.05 to 1)")]
    [Range(0.05f, 1f)]
    public float density = 0.1f;

    [Tooltip("Distance to offset path points from the surface")]
    public float offsetFromSurface = 0.1f;

    [Header("UI Controls")]
    [Tooltip("Button to generate path points")]
    public Button generatePathButton;

    [Tooltip("Button to start movement")]
    public Button startButton;

    [Tooltip("Container for path point objects")]
    public Transform pathPointsContainer;

    [Header("Line Renderer Settings")]
    [Tooltip("Show the path line in the scene")]
    public bool showPath = true;

    [Tooltip("Width of the path line")]
    public float lineWidth = 0.05f;

    [Tooltip("Color for passed path segments")]
    public Color passedColor = Color.red;

    [Tooltip("Color for unpassed path segments")]
    public Color unpassedColor = Color.green;

    [Tooltip("Color for the pulsing effect")]
    public Color pulseColor = Color.white;

    [Tooltip("Speed of the pulsing effect")]
    public float pulseSpeed = 2f;

    [Tooltip("Length of the pulse effect")]
    public float pulseLength = 0.3f;

    // === Private Fields ===
    private PathPointPool pointPool;           // Manages object pooling for path points
    private PathGenerator pathGenerator;       // Generates path points on the target mesh
    private PathSorter pathSorter;             // Sorts path points for traversal
    private PathMover pathMover;               // Controls mover object movement
    private PathLineRenderer pathLineRenderer; // Renders the path with visual effects
    private List<Vector3> orderedPath;         // Sorted list of path points

    // === Initialization ===
    // Sets up components and initializes the system.
    private void Awake()
    {
        // Initialize component dependencies
        pointPool = new PathPointPool(pathPointPrefab, pathPointsContainer);
        pathGenerator = new PathGenerator(targetObject, density, offsetFromSurface, pointPool, pathPointsContainer);
        pathSorter = new PathSorter3D(); // Can be swapped with PathSorter2D for 2D sorting
        pathMover = new PathMover(RobotEndPoint, RobotEndPoint.transform.position, EndPointMoveSpeed);
        pathLineRenderer = new PathLineRenderer(gameObject, lineWidth, showPath, passedColor, unpassedColor, pulseColor, pulseSpeed, pulseLength);

        // Initialize path storage
        orderedPath = new List<Vector3>();
    }

    // Assigns button click listeners.
    private void Start()
    {
        if (generatePathButton != null)
            generatePathButton.onClick.AddListener(GeneratePathPoints);

        if (startButton != null)
            startButton.onClick.AddListener(StartMoving);
    }

    // === Unity Lifecycle ===
    // Updates movement and line rendering each frame.
    private void Update()
    {
        pathMover.Update();
        pathLineRenderer.Update(orderedPath, pathMover.CurrentTargetIndex);
    }

    // Cleans up button listeners to prevent memory leaks.
    private void OnDestroy()
    {
        if (generatePathButton != null)
            generatePathButton.onClick.RemoveListener(GeneratePathPoints);

        if (startButton != null)
            startButton.onClick.RemoveListener(StartMoving);
    }

    // Detects Inspector changes in the Editor for real-time parameter tweaking.
    private void OnValidate()
    {
        // Only update if components are initialized
        if (pathMover != null && pathGenerator != null && pathLineRenderer != null)
        {
            UpdateComponentParameters();
        }
    }

    // === Private Methods ===
    // Updates component parameters to reflect Inspector changes.
    private void UpdateComponentParameters()
    {
        pathMover.UpdateParameters(EndPointMoveSpeed);
        pathGenerator.UpdateParameters(density, offsetFromSurface);
        pathLineRenderer.UpdateParameters(lineWidth, showPath, passedColor, unpassedColor, pulseColor, pulseSpeed, pulseLength);

        // Re-apply path visualization if needed
        if (orderedPath.Count > 0)
        {
            pathLineRenderer.SetPath(orderedPath);
        }
    }

    // === Public Methods ===
    // Generates path points and sorts them, resetting the mover and enabling the pulse effect.
    public void GeneratePathPoints()
    {
        pathMover.StopMoving();
        orderedPath = pathGenerator.GeneratePathPoints();
        orderedPath = pathSorter.Sort(orderedPath, showPath, gameObject.GetComponent<LineRenderer>(), unpassedColor);
        pathMover.SetPath(orderedPath);
        pathLineRenderer.SetPath(orderedPath);
        pathLineRenderer.StartPulsing();
    }

    // Starts the movement of the mover object along the path.
    public void StartMoving()
    {
        pathMover.StartMoving(pathLineRenderer);
    }
}