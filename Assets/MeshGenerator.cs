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

        public CycleFinder.ElementGroupPolygon sourcePolygon = null;
        public List<GameObject> polygonGameObjects = null;
        public List<Tuple<float, float>> polygonPositions = null;
        public Mesh mesh;
        public List<(int, int)> polygonMeshLinks;
        // public (float, float) meshMottomLeftCorner;
        //   public List<List<int>> sourceCycles = null;
    }

    public ConnectionSpringDrawer connectionSpringDrawer;
    // public Quaternion placementRot;
    public float placementZ;
    public float spring;
    public float damper;
    public bool allowRotation;
    public float meshSize;
    public int minStringSize;

    public static Mesh PolygonToMesh(List<Tuple<float, float>> polygonPositions, float meshSize) // , ValueTuple<float, float>? forceBLCorner = null
    {
        var extent = TopologyFunctions.ExtactExtent(polygonPositions);

        var (mesh, bottomLeftCorner) = TopologyFunctions.SimpleTriangleMesh(extent, meshSize);
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

    private string StringifyNodesLink(Mesh mesh, Tuple<int, int> link)
    {
        var pos1 = mesh.positions[link.Item1];
        var pos2 = mesh.positions[link.Item2];
        if (pos1.Item1 < pos2.Item1 || (pos1.Item1 == pos2.Item1 && pos1.Item2 < pos2.Item2))
        {
            return String.Format("{0},{1}_{2},{3}", pos1.Item1, pos1.Item2, pos2.Item1, pos2.Item2);
        }
        else
        {
            return String.Format("{0},{1}_{2},{3}", pos2.Item1, pos2.Item2, pos1.Item1, pos1.Item2);
        }
    }
    private string StringifyOuterLink(List<GameObject> gameObjects, Mesh mesh, ValueTuple<int, int> link)
    {
        var pos1 = mesh.positions[link.Item2];
        var go = gameObjects[link.Item2];
        return String.Format("{0}_{1},{2}", go.GetInstanceID(), pos1.Item1, pos1.Item2);
    }

    int tries = 0;
    public void CreateFea(List<CycleFinder.ElementGroupPolygon> polygons,
        Dictionary<int, GameObject> gameObjectsMap,
        GameObject spawnee,
        Quaternion spawneeRotation
        )
    {
        var feaContainer = GameObject.Find("FEA_data");
        if (SingleFEAData != null)
        {
            if (tries > 3) return;
            tries++;
            var FEMpolygons = polygons.Where(p => p.isTouchingExistingFEM);
            foreach (var touchingPolygon in polygons.Where(p => p.isTouchingExistingFEM))
            {
                var XX = touchingPolygon.polygon.Except(SingleFEAData.sourcePolygon.polygon).ToList();
                // if (touchingPolygon.polygon.Count >= minStringSize)

                var unifiedCycles = touchingPolygon.sourceCycles
                    .Concat(new List<List<int>>() {
                        SingleFEAData.sourcePolygon.polygon.GetRange(0, SingleFEAData.sourcePolygon.polygon.Count - 1)
                    }).ToList();
                var unifiedPolygons = CycleFinder.FindAdjacantCicles(unifiedCycles, x => false);



                if (unifiedPolygons.Count != 1)
                {
                    Debug.LogAssertion(string.Format("unifiedPolygons.Count != 1 --> {0}", unifiedPolygons.Count));
                    continue;
                }

                var unifiedPolygon = unifiedPolygons[0];

                List<GameObject> polygonGameObjects = unifiedPolygon.polygon.Select(ind => gameObjectsMap[ind]).ToList();
                List<Tuple<float, float>> newPolygonPositions = polygonGameObjects.Select(go => go.transform.position).Select(vec3 => Tuple.Create(vec3.x, vec3.y)).ToList();

                var polygonLastPositions = SingleFEAData.polygonPositions;
                var polygonCurrentPositions = SingleFEAData.polygonGameObjects.Select(go => go.transform.position).Select(vec3 => Tuple.Create(vec3.x, vec3.y)).ToList();

                var tranformMatrix = TopologyFunctions.LinearRegression2d(polygonLastPositions, polygonCurrentPositions);
                var tranformMatrixInv = tranformMatrix.inverse;

                var newPolygonPositions_t = newPolygonPositions.Select(pos => TopologyFunctions.TranformPoint(tranformMatrixInv, pos)).ToList();
                var mesh_t = PolygonToMesh(newPolygonPositions_t, meshSize); // ,SingleFEAData.meshMottomLeftCorner
                var polygonMeshLinks = TopologyFunctions.ConnectOuterPolygonToMesh(newPolygonPositions, mesh_t);

                // we need to understand :
                //    which polygon nodes should be removed
                //    which inner-mesh nodes should be added
                //    which inner-inner links should be added & removed
                //    which polygon-inner links should be added & removed

                var polygonNode_add = unifiedPolygon.polygon.Except(SingleFEAData.sourcePolygon.polygon);
                var polygonNode_remove = SingleFEAData.sourcePolygon.polygon.Except(unifiedPolygon.polygon);

                var innerMesh_add = mesh_t.positions.Except(SingleFEAData.mesh.positions);
                var innerMesh_remove = SingleFEAData.mesh.positions.Except(mesh_t.positions);

                // similar links need to be distinguished by their's position (and not index)
                var stringifiedExistingInnerLinks = SingleFEAData.mesh.links.Select(x => StringifyNodesLink(SingleFEAData.mesh, x));//.Except();
                var stringifiedNewInnerLinks = mesh_t.links.Select(x => StringifyNodesLink(mesh_t, x));//.Except();
                var innerLinks_add = stringifiedNewInnerLinks.Except(stringifiedExistingInnerLinks);
                var innerLinks_remove = stringifiedExistingInnerLinks.Except(stringifiedNewInnerLinks);

                // similar links need to be distinguished by their's position (and not index)
                var existingOuterLinksDic = SingleFEAData.polygonMeshLinks.ToDictionary(x => x, x => StringifyOuterLink(SingleFEAData.polygonGameObjects, SingleFEAData.mesh, x));
                var newOuterLinksDic = polygonMeshLinks.ToDictionary(x => x, x => StringifyOuterLink(polygonGameObjects, mesh_t, x));
                var existingOuterLinksDic_rev = existingOuterLinksDic.ToDictionary(t => t.Value, t => t.Key);
                var newOuterLinksDic_rev = newOuterLinksDic.ToDictionary(t => t.Value, t => t.Key);

                var stringifiedExistingOuterLinks = SingleFEAData.polygonMeshLinks.Select(x => existingOuterLinksDic[x]);//.Except();
                var stringifiedNewOuterLinks = polygonMeshLinks.Select(x => newOuterLinksDic[x]);
                var outerLinks_add = stringifiedNewOuterLinks.Except(stringifiedExistingOuterLinks).Select(str => newOuterLinksDic_rev[str]);
                var outerLinks_remove = stringifiedExistingOuterLinks.Except(stringifiedNewOuterLinks).Select(str => existingOuterLinksDic_rev[str]);

                Debug.Log(string.Format("{0},{1}", polygonNode_add.Count(), polygonNode_remove.Count()));
                // TRANFORM !

                //    foreach (var position in mesh.positions)
                //    {
                //        var go = Instantiate(spawnee, new Vector3(position.Item1, position.Item2, placementZ), spawneeRotation, feaContainer.transform);
                //        go.transform.localScale = new Vector3(0.2f, 1, 0.2f);

                //        if (!allowRotation)
                //        {
                //            var rb = go.GetComponent<Rigidbody>();
                //            rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
                //        }

                //        go.tag = Tags.FEM_CENTER_PARTICLE;

                //        SingleFEAData.elements.Add(go);
                //    }
                //    foreach (var connection in mesh.links)
                //    {
                //        this.connectionSpringDrawer.AddConnection(
                //            SingleFEAData.elements[connection.Item1],
                //            SingleFEAData.elements[connection.Item2]
                //            );
                //    }
                //    foreach (var (polygonConnectionIndex, meshConnectionIndex) in polygonMeshLinks)
                //    {
                //        this.connectionSpringDrawer.AddConnection(
                //            polygonGameObjects[polygonConnectionIndex],
                //            SingleFEAData.elements[meshConnectionIndex]
                //            );
                //    }

                //    polygon.holes
                //        .SelectMany(x => x)
                //        .Concat(polygon.restElements)
                //        .Select(x => gameObjectsMap[x])
                //        .ToList()
                //        .ForEach(x => x.SetActive(false));

                //    polygon.polygon
                //        .Select(x => gameObjectsMap[x])
                //        .ToList()
                //        .ForEach(node => node.tag = Tags.FEM_EDGE_PARTICLE);

                //    SingleFEAData = new ElementGroupGameObject()
                //    {
                //        sourcePolygon = polygon,
                //        polygonGameObjects = polygonGameObjects,
                //        meshMottomLeftCorner = bottomLeftCorner
                //    }; // , sourceCycles = polygon.sourceCycles

                //    return;

                //}
            }
        }
        else
        {
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

                    SingleFEAData = new ElementGroupGameObject()
                    {
                        mesh = mesh,
                        sourcePolygon = polygon,
                        polygonGameObjects = polygonGameObjects,
                        polygonPositions = polygonPositions,
                        polygonMeshLinks = polygonMeshLinks,
                        // meshMottomLeftCorner = bottomLeftCorner
                    }; // , sourceCycles = polygon.sourceCycles


                    foreach (var position in mesh.positions)
                    {
                        SingleFEAData.elements.Add(CreateInnerMesh(position, spawnee, spawneeRotation, feaContainer.transform));
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

                    return;
                }
            }
        }
    }

    private GameObject CreateInnerMesh(Tuple<float, float> position, GameObject spawnee, Quaternion spawneeRotation, Transform transform)
    {
        var go = Instantiate(spawnee, new Vector3(position.Item1, position.Item2, placementZ), spawneeRotation, transform);
        go.transform.localScale = new Vector3(0.2f, 1, 0.2f);

        if (!allowRotation)
        {
            var rb = go.GetComponent<Rigidbody>();
            rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        go.tag = Tags.FEM_CENTER_PARTICLE;
        return go;
    }

    // Update is called once per frame
    void Update()
    {

    }
};
