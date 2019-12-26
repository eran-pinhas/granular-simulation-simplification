// #define CollisionType GameObject


using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class FEA : MonoBehaviour, ICollisionListener
{

    public GameObject spawnee;
    public Transform pos;
    public Reporter reporter;
    public MeshGenerator meshGenerator;
    public ConnectionDrawer connectionDrawer;
    public float MaxForce;

    private List<GameObject> children = new List<GameObject>();
    private Dictionary<int, GameObject> childrenDict = new Dictionary<int, GameObject>();

    void Start()
    {
        new GameObject("FEA_data");
    }

    void Update()
    {
        var maxs = meshGenerator.MonitorMesh(childrenDict);
        var maxMaxPull = maxs.Select(m => m.Item2).DefaultIfEmpty(-1).Max();
        reporter.reportNew(maxMaxPull > float.MinValue ? maxMaxPull : -1, Time.time);
        if (maxMaxPull > MaxForce)
        {
            // meshGenerator.StartCrackPropagation(maxPullJoint);
        }

        var cycles = CycleFinder.Find<bool>(childrenDict.Select(t => t.Key), collisions, 3);
        try
        {
            var adj = CycleFinder.FindAdjacantCicles(cycles, nodeId => childrenDict[nodeId].tag == Tags.FEM_EDGE_PARTICLE, instanceId => MeshGenerator.getGameObjectPosition(childrenDict[instanceId]));
            this.meshGenerator.CreateFea(adj, childrenDict, spawnee, pos.rotation);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }

    }

    public void informNewChild(GameObject obj)
    {
        children.Add(obj);
        childrenDict.Add(obj.GetInstanceID(), obj);
        collisions.Add(obj.GetInstanceID(), new Dictionary<int, bool>());
    }

    public Dictionary<int, Dictionary<int, bool>> collisions = new Dictionary<int, Dictionary<int, bool>>();
    public void informCollision(GameObject a, GameObject b)
    {
        collisions[a.GetInstanceID()].Add(b.GetInstanceID(), true); // , line
        this.connectionDrawer.AddConnection(a, b);
    }

    public void informCollisionRemoved(GameObject a, GameObject b)
    {
        this.connectionDrawer.RemoveConnection(a, b);
        collisions[a.GetInstanceID()].Remove(b.GetInstanceID());
    }
}
