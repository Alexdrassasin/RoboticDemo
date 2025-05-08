using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class PathMover
{
    private readonly GameObject moverObject;
    private readonly Vector3 initialPosition;
    private float speed;
    private List<Vector3> path;
    private int currentTargetIndex = 0;
    private bool canMove = false;
    private Tween moveToStartTween;

    public PathMover(GameObject moverObject, Vector3 initialPosition, float speed)
    {
        this.moverObject = moverObject;
        this.initialPosition = initialPosition;
        this.speed = speed;
        this.path = new List<Vector3>();
    }

    public void SetPath(List<Vector3> path)
    {
        this.path = path;
    }

    public void Update()
    {
        if (!canMove) return;

        if (path.Count > 0 && currentTargetIndex < path.Count)
        {
            Vector3 targetPosition = path[currentTargetIndex];
            moverObject.transform.position = Vector3.MoveTowards(moverObject.transform.position, targetPosition, speed * Time.deltaTime);

            if (Vector3.Distance(moverObject.transform.position, targetPosition) < 0.1f)
            {
                currentTargetIndex++;
            }
        }
        else
        {
            canMove = false;
            float travelTime = Vector3.Distance(moverObject.transform.position, initialPosition) / speed;
            moveToStartTween = moverObject.transform.DOMove(initialPosition, travelTime, false).SetEase(Ease.Linear);
        }
    }

    public void StartMoving(PathLineRenderer lineRenderer)
    {
        canMove = false;
        moveToStartTween?.Kill();
        moveToStartTween = null;
        currentTargetIndex = 0;
        Vector3 startPosition = path.Count > 0 ? path[0] : initialPosition;
        float travelTime = Vector3.Distance(moverObject.transform.position, startPosition) / speed;

        lineRenderer.ResetPulse();
        lineRenderer.SetGradientToUnpassed();

        moveToStartTween = moverObject.transform.DOMove(startPosition, travelTime, false).SetEase(Ease.Linear).OnComplete(() =>
        {
            canMove = true;
        });
    }

    public void StopMoving()
    {
        canMove = false;
        moveToStartTween?.Kill();
        moveToStartTween = null;
        float travelTime = Vector3.Distance(moverObject.transform.position, initialPosition) / speed;
        moveToStartTween = moverObject.transform.DOMove(initialPosition, travelTime, false).SetEase(Ease.Linear);
    }

    // Updates parameters to reflect real-time changes.
    public void UpdateParameters(float newSpeed)
    {
        this.speed = newSpeed;
    }

    public int CurrentTargetIndex => currentTargetIndex;
}