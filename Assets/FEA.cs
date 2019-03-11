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
    public Vector3 a, b, collisionRenderOffset;
    public float spring;
    public float damper;
    public bool allowRoration;
    public bool plotLine;
    public float coloringMaxPush;
    public float coloringMaxPull;
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

    private const float pushHColor = 0.666f;
    private const float pullHColor = 0;

    // Start is called before the first frame update
    void Start()
    {
        Quaternion rot = pos.rotation;
        List<Vector3> positions = new List<Vector3>()
        {
            //     pos.position, pos.position + a, pos.position + b, pos.position + a + b
        };
        connections = new List<Vector2Int>
        {
            //new Vector2Int(0,1),
            //new Vector2Int(0,2),
            //new Vector2Int(0,3),
            //new Vector2Int(1,2),
            //new Vector2Int(1,3),
            //new Vector2Int(2,3)
        };
        var feaContainer = new GameObject("FEA_data");
        feaContainer.transform.parent = this.gameObject.transform;


        foreach (var position in positions)
        {
            var go = Instantiate(spawnee, position, rot, feaContainer.transform);
            if (!allowRoration)
            {
                var rb = go.GetComponent<Rigidbody>();
                rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            }
            objects.Add(go);
        }
        foreach (var connection in connections)
        {
            var sj = objects[connection.x].AddComponent<SpringJoint>();
            sj.connectedBody = objects[connection.y].GetComponent<Rigidbody>();
            sj.spring = this.spring;
            sj.damper = this.damper;
            connections_spring.Add(sj);
            if (plotLine)
            {
                var line = new GameObject("spring-" + connection.x + "-" + connection.y);
                var lr = line.AddComponent<LineRenderer>();
                lr.startWidth = 0.05f;
                lr.endWidth = 0.05f;

                lines.Add(line);
                line.transform.parent = feaContainer.transform;
            }
        }
    }

    void Update()
    {
        float maxPull = float.MinValue;
        for (int i = 0; i < connections.Count; i++)
        {
            var connection = connections[i];
            var con_spring = connections_spring[i];

            if (plotLine)
            {
                var line = lines[i];

                var lr = line.GetComponent<LineRenderer>();
                lr.SetPosition(0, objects[connection.x].GetComponent<Transform>().position);
                lr.SetPosition(1, objects[connection.y].GetComponent<Transform>().position);

                float anchorLength = (con_spring.anchor - con_spring.connectedAnchor).magnitude;
                float currentLength = (objects[connection.x].GetComponent<Transform>().position - objects[connection.y].GetComponent<Transform>().position).magnitude;

                Color col;

                // Debug.Log(force + " " + colorS);
                if (currentLength < anchorLength)
                {
                    float force = (anchorLength - currentLength) * con_spring.spring;
                    float colorS = Mathf.Min(1, force / coloringMaxPush);
                    col = Color.HSVToRGB(pushHColor, colorS, 1);
                }
                else
                {
                    float force = (currentLength - anchorLength) * con_spring.spring;
                    float colorS = Mathf.Min(1, force / coloringMaxPull);
                    col = Color.HSVToRGB(pullHColor, colorS, 1);

                    if (force > maxPull)
                    {
                        maxPull = force;
                    }
                }
                lr.material.color = col;
            }
        }

        reporter.reportNew(maxPull > float.MinValue ? maxPull : -1, Time.time);

        var cycles = CycleFinder.Find<bool>(childrenDict.Select(t => t.Key), collisions, 3);
        try
        {
            var adj = CycleFinder.FindAdjacantCicles(cycles, nodeId => childrenDict[nodeId].tag == Tags.FEM_EDGE_PARTICLE);
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
