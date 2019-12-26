using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System;

public class LineDrawer
{

    public static Vector3 getGameObjectPosition(GameObject go)
    {
        var vec3 = go.transform.position;
        return vec3; // Tuple.Create(vec3.x, vec3.y);
    }

    LineRenderer lr;
    Vector3 shift;
    public LineDrawer(Vector3 shift , Color color)
    {
        this.shift = shift;

        var line = new GameObject("line-drawer-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        lr = line.AddComponent<LineRenderer>();
        lr.startWidth = 0.5f;
        lr.endWidth = 0.5f;
        lr.startColor = color;
        lr.endColor = color;
        lr.material.color = color;

        line.transform.parent = GameObject.Find("DrawedLines").transform;
    }

    private List<GameObject> points = new List<GameObject>();

    public void updatePositions()
    {
        var positions = points.Select(x => getGameObjectPosition(x) + shift);
        lr.SetPositions(positions.ToArray());
    }
    public void setPoints(IEnumerable<GameObject> l)
    {
        points.Clear();
        points.AddRange(l);
        lr.positionCount = points.Count;
    }
}
