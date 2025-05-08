using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PathPointPool
{
    private readonly Queue<GameObject> pool = new Queue<GameObject>(); 
    private readonly GameObject prefab; 
    private readonly Transform container;

    public PathPointPool(GameObject prefab, Transform container)
    {
        this.prefab = prefab;
        this.container = container;
    }

    public GameObject Get()
    {
        if (pool.Count > 0)
        {
            GameObject point = pool.Dequeue();
            point.SetActive(true);
            return point;
        }
        return Object.Instantiate(prefab, container);
    }

    public void Return(GameObject point)
    {
        point.SetActive(false);
        pool.Enqueue(point);
    }

    public void Clear(Transform container)
    {
        foreach (Transform child in container)
        {
            Return(child.gameObject);
        }
    }
}