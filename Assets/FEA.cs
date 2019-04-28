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
    //public float spring;
    //public float damper;
    //public bool allowRoration;
    //public bool plotLine;
    //public float coloringMaxPush;
    //public float coloringMaxPull;
    public Reporter reporter;
    public MeshGenerator meshGenerator;
    public ConnectionDrawer connectionDrawer;

    private Quaternion rot;
    private List<Vector3> positions;
    private List<Vector2Int> connections;
    private List<SpringJoint> connections_spring = new List<SpringJoint>();
    private List<GameObject> objects = new List<GameObject>();
    
    private List<GameObject> children = new List<GameObject>();
    private Dictionary<int, GameObject> childrenDict = new Dictionary<int, GameObject>();
    private List<GameObject> lines = new List<GameObject>();
    
    void Start()
    {
        new GameObject("FEA_data");
    }

    void Update()
    {
        float maxPull = meshGenerator.MonitorMesh(childrenDict);
        
        reporter.reportNew(maxPull > float.MinValue ? maxPull : -1, Time.time);

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
