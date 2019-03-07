using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;


public class MeshGenerator : MonoBehaviour
{
    class ElementGroupGameObject
    {
        public List<GameObject> elements = new List<GameObject>();
        public List<SpringJoint> links = new List<SpringJoint>();
        public List<SpringJoint> meshPolygonLinks = new List<SpringJoint>();
        public List<GameObject> linkPlots = new List<GameObject>();
    }

    public ConnectionSpringDrawer connectionSpringDrawer;
    // public Quaternion placementRot;
    public float placementZ;
    public float spring;
    public float damper;
    public bool allowRotation;
    public float meshSize;
    public int minStringSize;

    public static Mesh PolygonToMesh(List<Tuple<float, float>> polygonPositions, float meshSize)
    {
        var extent = TopologyFunctions.ExtactExtent(polygonPositions);

        Mesh mesh = TopologyFunctions.SimpleTriangleMesh(extent, meshSize);
        var indicesToStay = new Dictionary<int, int>();
        var newIndex = 0;
        for (var i = 0; i < mesh.positions.Count; i++)
            if (TopologyFunctions.PointInPolygon(polygonPositions, mesh.positions[i]))
                indicesToStay[i] = newIndex++;

        mesh.positions = mesh.positions.Where((pos, i) => indicesToStay.ContainsKey(i)).ToList();
        mesh.links = mesh.links.Where(t => indicesToStay.ContainsKey(t.Item1) && indicesToStay.ContainsKey(t.Item2))
            .Select(t => Tuple.Create(indicesToStay[t.Item1], indicesToStay[t.Item2])).ToList();

        return mesh;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    private bool DoneSingleFEA = false;
    private ElementGroupGameObject SingleFEAData = null;

    private SpringJoint CreateSpring(GameObject a, GameObject b, float stiffness, float damping, bool visulized, string name, Transform parent)
    {
        var sj = a.AddComponent<SpringJoint>();
        sj.connectedBody = b.GetComponent<Rigidbody>();
        sj.spring = stiffness;
        sj.damper = damping;
        SingleFEAData.links.Add(sj);

        if (visulized)
        {
            var line = new GameObject(name);
            var lr = line.AddComponent<LineRenderer>();
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;

            SingleFEAData.linkPlots.Add(line);
            line.transform.parent = parent;
        }
        return sj;
    }

    public void CreateFea(List<CycleFinder.ElementGroupPolygon> polygons, Dictionary<int, GameObject> gameObjectsMap, GameObject spawnee, Quaternion spawneeRotation)
    {
        if (DoneSingleFEA) return;
        foreach (var polygon in polygons)
        {
            if (polygon.polygon.Count >= minStringSize)
            {
                List<GameObject> polygonGameObjects = polygon.polygon.Select(ind => gameObjectsMap[ind]).ToList();
                List<Tuple<float, float>> polygonPositions = polygonGameObjects.Select(go => go.transform.position).Select(vec3 => Tuple.Create(vec3.x, vec3.y)).ToList();

                var mesh = PolygonToMesh(polygonPositions, meshSize);

                if (polygon.restElements.Count * 1.5 < polygon.polygon.Count)
                    return;

                var polygonMeshLinks = TopologyFunctions.ConnectOuterPolygonToMesh(polygonPositions, mesh);

                var feaContainer = GameObject.Find("FEA_data");
                feaContainer.transform.parent = this.gameObject.transform;

                SingleFEAData = new ElementGroupGameObject();


                foreach (var position in mesh.positions)
                {
                    var go = Instantiate(spawnee, new Vector3(position.Item1, position.Item2, placementZ), spawneeRotation, feaContainer.transform);
                    go.transform.localScale = new Vector3(0.2f, 1, 0.2f);

                    if (!allowRotation)
                    {
                        var rb = go.GetComponent<Rigidbody>();
                        rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
                    }

                    go.tag = Tags.FEM_CENTER_PARTICLE;

                    SingleFEAData.elements.Add(go);
                }
                foreach (var connection in mesh.links)
                {
                    this.connectionSpringDrawer.AddConnection(
                        SingleFEAData.elements[connection.Item1],
                        SingleFEAData.elements[connection.Item2]
                        );
                }
                foreach (var (polygonConnectionIndex, meshConnectionIndex) in polygonMeshLinks)
                {
                    this.connectionSpringDrawer.AddConnection(
                        polygonGameObjects[polygonConnectionIndex],
                        SingleFEAData.elements[meshConnectionIndex]
                        );
                }

                polygon.holes
                    .SelectMany(x => x)
                    .Concat(polygon.restElements)
                    .Select(x => gameObjectsMap[x])
                    .ToList()
                    .ForEach(x => x.SetActive(false));

                polygon.polygon
                    .Select(x => gameObjectsMap[x])
                    .ToList()
                    .ForEach(node => node.tag = Tags.FEM_EDGE_PARTICLE);

                DoneSingleFEA = true;
                return;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
};
