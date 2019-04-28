using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class ConnectionDrawer : MonoBehaviour
{
    Transform parentTransform;
    Dictionary<(int, int), GameObject> connections = new Dictionary<(int, int), GameObject>();
    Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();

    public bool ColorStresses;
    public Vector3 drawOffset;

    virtual public void AddConnection(GameObject a, GameObject b)
    {
        var line = new GameObject("drawed-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        var lr = line.AddComponent<LineRenderer>();
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;

        line.transform.parent = parentTransform;

        connections.Add((a.GetInstanceID(), b.GetInstanceID()), line);
        objects[b.GetInstanceID()] = b;
        objects[a.GetInstanceID()] = a;
    }

    public void SetColor(GameObject a, GameObject b, Color color)
    {
        var lr = connections[(a.GetInstanceID(), b.GetInstanceID())].GetComponent<LineRenderer>();
        lr.material.color = color;
    }

    virtual public bool RemoveConnection(GameObject a, GameObject b)
    {
        var aId = a.GetInstanceID();
        var bId = b.GetInstanceID();
        if (connections.ContainsKey((aId, bId)))
        {
            Destroy(connections[(aId, bId)]);

            connections.Remove((aId, bId));

            if (!connections.Any(kv => kv.Key.Item1 == aId || kv.Key.Item2 == aId)) objects.Remove(aId);
            if (!connections.Any(kv => kv.Key.Item1 == bId || kv.Key.Item2 == bId)) objects.Remove(bId);

            return true;
        }
        else
        {
            return false;
        }
    }
    // Start is called before the first frame update
    virtual public void Start()
    {
        var drawContainer = GameObject.Find("DrawObjects");
        if (drawContainer == null)
        {
            drawContainer = new GameObject("DrawObjects");
        }
        parentTransform = drawContainer.transform;
    }

    // Update is called once per frame
    virtual public void Update()
    {
        foreach (var keyVal in connections)
        {
            var (aId, bId) = keyVal.Key;
            var line = keyVal.Value;
            var lr = line.GetComponent<LineRenderer>();

            lr.SetPosition(0, objects[bId].GetComponent<Transform>().position + drawOffset);
            lr.SetPosition(1, objects[aId].GetComponent<Transform>().position + drawOffset);
        }
    }
}
