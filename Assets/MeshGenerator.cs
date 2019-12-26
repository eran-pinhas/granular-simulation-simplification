using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;


public class MeshGenerator : MonoBehaviour
{
    public static Tuple<float, float> getGameObjectPosition(GameObject go)
    {
        var vec3 = go.transform.position;
        return Tuple.Create(vec3.x, vec3.y);
    }

    private static void setGameObjectPosition(GameObject go, Tuple<float, float> pos)
    {
        Vector3 vec3 = new Vector3(pos.Item1, pos.Item2, go.transform.position.z);
        go.transform.position = vec3;
    }

    public class ElementGroupGameObject
    {
        public class HiddenNodes
        {
            public GameObject gameObject;
            public Tuple<float, float> lastPosition;
        }

        public class InnerSpringJoint
        {
            public ValueTuple<GameObject, GameObject> objs;
            public Tuple<float, float> fromPoint;
            public Tuple<float, float> toPoint;
        }
        public class OuterSpringJoint
        {
            public ValueTuple<GameObject, GameObject> objs;
            public GameObject fromObject;
            public Tuple<float, float> toPoint;
        }
        public class PolygonElement
        {
            public int instanceId;
            public Tuple<float, float> positionsInRootRS; // ReferenceSystem
        }

        public enum STATUS
        {
            FREE = 0,
            CRACKING = 1,
        }

        public List<HiddenNodes> hiddenNodes = new List<HiddenNodes>();
        public List<OuterSpringJoint> outerLinks = new List<OuterSpringJoint>();
        public List<InnerSpringJoint> innerLinks = new List<InnerSpringJoint>();
        public Dictionary<Tuple<float, float>, GameObject> innerMeshElements = new Dictionary<Tuple<float, float>, GameObject>();
        public List<PolygonElement> currentPolygon;
        public STATUS status = STATUS.FREE;
        public LineDrawer lineDrawer;

        public Color color;

        public ElementGroupGameObject()
        {
            color = Color.HSVToRGB(UnityEngine.Random.value, 0.73f, 0.96f);
            lineDrawer = new LineDrawer(new Vector3(0, 0, -10), color);
        }
    }

    public ConnectionSpringDrawer connectionSpringDrawer;
    public float placementZ;
    public float spring;
    public float damper;
    public bool allowRotation;
    public float meshSize;
    public float bufferInside;
    public int minStringSize;
    public float PPTestMin;
    public static Mesh PolygonToMesh(List<Tuple<float, float>> polygonPositions, float meshSize, float bufferInside)
    {
        var extent = TopologyFunctions.ExtactExtent(polygonPositions);

        var (mesh, bottomLeftCorner) = TopologyFunctions.SimpleTriangleMesh(extent, meshSize);
        var indicesToStay = new Dictionary<int, int>();
        var newIndex = 0;
        for (var i = 0; i < mesh.positions.Count; i++)
            if (TopologyFunctions.PointInPolygon(polygonPositions, mesh.positions[i], bufferInside))
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

    private List<ElementGroupGameObject> FEAs = new List<ElementGroupGameObject>();

    private (Tuple<float, float>, Tuple<float, float>) StringifyNodesLink(Tuple<float, float> pos1, Tuple<float, float> pos2) // (Mesh mesh, Tuple<int, int> link)
    {
        if (pos1.Item1 < pos2.Item1 || (pos1.Item1 == pos2.Item1 && pos1.Item2 < pos2.Item2))
        {
            return (pos1, pos2);
        }
        else
        {
            return (pos2, pos1);
        }
    }

    int tries = 0;
    public void CreateFea(List<CycleFinder.ElementGroupPolygon> polygons,
        Dictionary<int, GameObject> gameObjectsMap,
        GameObject spawnee,
        Quaternion spawneeRotation
        )
    {
        var feaContainer = GameObject.Find("FEA_data");
        var changed = false;

        // Maintain existing FEAs

        FEAs.ForEach(fea =>
        {
            if (fea.status == ElementGroupGameObject.STATUS.CRACKING)
                return;

            foreach (var touchingPolygon in polygons.Where(p => p.isTouchingExistingFEM))
            {
                if (changed) return;
                //  if (tries > 0) return;

                if (touchingPolygon.sourceCycles.Count < 5) continue;

                //Debug.Log(SingleFEAData.currentPolygon.GetRange(0, SingleFEAData.currentPolygon.Count - 1).Select(x => x.instanceId).ToList());
                var unifiedCycles = touchingPolygon.sourceCycles
                    .Concat(new List<List<int>>() {
                        fea.currentPolygon.GetRange(0, fea.currentPolygon.Count - 1).Select(x=>x.instanceId).ToList()
                    }).ToList();
                var unifiedPolygons = CycleFinder.FindAdjacantCicles(unifiedCycles, x => false, instanceId => getGameObjectPosition(gameObjectsMap[instanceId]));



                if (unifiedPolygons.Count != 1)
                {
                    //Debug.LogAssertion(string.Format("unifiedPolygons.Count != 1 --> {0}", unifiedPolygons.Count));
                    continue;
                }
                tries++;

                var unifiedPolygon = unifiedPolygons[0];

                List<GameObject> polygonGameObjects = unifiedPolygon.polygon.Select(ind => gameObjectsMap[ind]).ToList();
                List<Tuple<float, float>> newPolygonPositions = polygonGameObjects.Select(go => getGameObjectPosition(go)).ToList();

                if (TopologyFunctions.PolsbyPopper(newPolygonPositions) < PPTestMin)
                    continue;

                var polygonLastPositions = fea.currentPolygon.Select(x => x.positionsInRootRS);
                var polygonCurrentPositions = fea.currentPolygon.Select(node => getGameObjectPosition(gameObjectsMap[node.instanceId]));

                var tranformMatrix = TopologyFunctions.LinearRegression2d(polygonLastPositions, polygonCurrentPositions);
                var tranformMatrixInv = tranformMatrix.inverse;

                var newPolygonPositions_t = newPolygonPositions.Select(pos => TopologyFunctions.TranformPoint(tranformMatrixInv, pos)).ToList();
                var mesh_t = PolygonToMesh(newPolygonPositions_t, meshSize, bufferInside);
                var polygonMeshLinks = TopologyFunctions.ConnectOuterPolygonToMesh(newPolygonPositions, mesh_t);

                // we need to understand :
                //    which polygon nodes should be removed & added        (1)
                //    which inner-mesh nodes should be removed & added     (2)
                //    which inner-inner links should be added & removed    (3)
                //    which polygon-inner links should be added & removed  (4)

                // (1)
                var currentPolygonIds = fea.currentPolygon.Select(n => n.instanceId);
                var polygonNode_add = unifiedPolygon.polygon.Except(currentPolygonIds);
                var polygonNode_remove = currentPolygonIds.Except(unifiedPolygon.polygon);
                var restElements_remove = unifiedPolygon.getNonPolygonElements().Except(currentPolygonIds);

                // (2)
                var prev_pos = fea.innerMeshElements.Select(x => x.Key);
                var new_pos = mesh_t.positions;
                var innerMesh_add = new_pos.Except(prev_pos);
                var innerMesh_remove = prev_pos.Except(new_pos);

                // (3)
                // similar links need to be distinguished by their's position (and not index)
                var stringifiedExistingInnerLinks = fea.innerLinks.Select(x => StringifyNodesLink(x.fromPoint, x.toPoint));
                var stringifiedNewInnerLinks = mesh_t.links.Select(x => StringifyNodesLink(mesh_t.positions[x.Item1], mesh_t.positions[x.Item2]));
                var innerLinks_add = stringifiedNewInnerLinks.Except(stringifiedExistingInnerLinks);
                var innerLinks_remove = stringifiedExistingInnerLinks.Except(stringifiedNewInnerLinks);

                // (4)
                var stringifiedExistingOuterLinks = fea.outerLinks.Select(x => (x.fromObject.GetInstanceID(), x.toPoint));
                var stringifiedNewOuterLinks = polygonMeshLinks.Select(link => (polygonGameObjects[link.Item1].GetInstanceID(), mesh_t.positions[link.Item2]));
                var outerLinks_add = stringifiedNewOuterLinks.Except(stringifiedExistingOuterLinks);
                var outerLinks_remove = stringifiedExistingOuterLinks.Except(stringifiedNewOuterLinks);


                if (polygonNode_add.Any() ||
                            polygonNode_remove.Any() ||
                            restElements_remove.Any() ||
                            innerMesh_add.Any() ||
                            innerMesh_remove.Any() ||
                            innerLinks_add.Any() ||
                            innerLinks_remove.Any() ||
                            outerLinks_add.Any() ||
                            outerLinks_remove.Any()
                ) changed = true;
                else
                {
                    UnityEngine.Debug.Log("no changes recorded");
                    continue;
                }

                // (4)
                foreach (var (polygonElemId, meshElemPos) in outerLinks_remove.ToList())
                {
                    var polygonGO = gameObjectsMap[polygonElemId];
                    var linkToRemove = fea.outerLinks.First(j => j.fromObject == polygonGO && j.toPoint == meshElemPos);

                    this.connectionSpringDrawer.RemoveConnection(linkToRemove.objs.Item1, linkToRemove.objs.Item2);

                    fea.outerLinks.Remove(linkToRemove);
                }
                // (3)
                foreach (var (p1, p2) in innerLinks_remove.ToList())
                {
                    var linkToRemove = fea.innerLinks.First(j => (j.fromPoint == p1 && j.toPoint == p2) || (j.fromPoint == p2 && j.toPoint == p1));

                    this.connectionSpringDrawer.RemoveConnection(linkToRemove.objs.Item1, linkToRemove.objs.Item2);

                    fea.innerLinks.Remove(linkToRemove);
                }
                // (2)
                foreach (var innerMashPos in innerMesh_remove.ToList())
                {
                    var go = fea.innerMeshElements[innerMashPos];
                    Destroy(go);
                    fea.innerMeshElements.Remove(innerMashPos);
                }
                // (1)
                foreach (var go in polygonNode_remove.Select(nodeId => gameObjectsMap[nodeId]))
                {
                    go.SetActive(false);
                    fea.hiddenNodes.Add(new ElementGroupGameObject.HiddenNodes()
                    {
                        gameObject = go,
                        lastPosition = TopologyFunctions.TranformPoint(tranformMatrixInv, getGameObjectPosition(go)),
                    });
                }
                foreach (var instanceId in restElements_remove)
                {
                    var go = gameObjectsMap[instanceId];
                    go.SetActive(false);
                    fea.hiddenNodes.Add(new ElementGroupGameObject.HiddenNodes()
                    {
                        gameObject = go,
                        lastPosition = TopologyFunctions.TranformPoint(tranformMatrixInv, getGameObjectPosition(go)),
                    });
                }

                // (1)
                foreach (var nodeId in polygonNode_add)
                {
                    var go = gameObjectsMap[nodeId];
                    go.tag = Tags.FEM_EDGE_PARTICLE;

                    Tuple<float, float> pos = null;
                    fea.hiddenNodes.Where(n => n.gameObject == go).ToList().ForEach(hiddenNode =>
                    {
                        setGameObjectPosition(go, TopologyFunctions.TranformPoint(tranformMatrix, hiddenNode.lastPosition));
                        pos = hiddenNode.lastPosition;
                        fea.hiddenNodes.Remove(hiddenNode);
                        go.SetActive(true);
                    });

                    if (pos == null)
                    {
                        pos = TopologyFunctions.TranformPoint(tranformMatrixInv, getGameObjectPosition(go));
                    }

                    fea.currentPolygon.Add(new ElementGroupGameObject.PolygonElement()
                    {
                        instanceId = nodeId,
                        positionsInRootRS = pos,
                    });
                }

                // reorganize polygon order
                var currentPolygonDic = new Dictionary<int, ElementGroupGameObject.PolygonElement>();
                fea.currentPolygon.ForEach(n => currentPolygonDic[n.instanceId] = n);
                fea.currentPolygon = unifiedPolygon.polygon.Select(instanceId => currentPolygonDic[instanceId]).ToList();

                // (2)
                foreach (var p_t in innerMesh_add)
                {
                    var position = TopologyFunctions.TranformPoint(tranformMatrix, p_t);
                    fea.innerMeshElements.Add(p_t, CreateInnerMesh(TopologyFunctions.TranformPoint(tranformMatrix, position), spawnee, spawneeRotation, feaContainer.transform));
                }
                //Debug.Log(fea.innerMeshElements.Keys.Select(x => x).ToList());
                // (3)
                foreach (var (p1, p2) in innerLinks_add)
                {
                    //Debug.Log(p1 + " " + p2);
                    var p1GO = fea.innerMeshElements[p1];
                    var p2GO = fea.innerMeshElements[p2];

                    fea.innerLinks.Add(new ElementGroupGameObject.InnerSpringJoint()
                    {
                        fromPoint = p1,
                        toPoint = p2,
                        objs = (p1GO, p2GO),
                    });
                    this.connectionSpringDrawer.AddConnectionWithAnchor(p1GO, p2GO, p1, p2);
                }
                // (4)
                foreach (var (goId, meshElemPos) in outerLinks_add)
                {
                    var meshElemGO = fea.innerMeshElements[meshElemPos];
                    var polyGO = gameObjectsMap[goId];
                    fea.outerLinks.Add(new ElementGroupGameObject.OuterSpringJoint
                    {
                        fromObject = polyGO,
                        toPoint = meshElemPos,
                        objs = (polyGO, meshElemGO),
                    });
                    this.connectionSpringDrawer.AddConnection(polyGO, meshElemGO);
                }
                Debug.Log(string.Format("DONE {0},{1}", polygonNode_add.Count(), polygonNode_remove.Count()));
            }

        });
        foreach (var polygon in polygons)
        {
            if (!changed && polygon.polygon.Count >= minStringSize)
            {
                // Create new FEA model
                List<GameObject> polygonGameObjects = polygon.polygon.Select(ind => gameObjectsMap[ind]).ToList();
                List<Tuple<float, float>> polygonPositions = polygonGameObjects.Select(go => getGameObjectPosition(go)).ToList();

                var mesh = PolygonToMesh(polygonPositions, meshSize, bufferInside);

                if (polygon.restElements.Count * 1.5 < polygon.polygon.Count)
                    return;
                if (TopologyFunctions.PolsbyPopper(polygonPositions) < PPTestMin)
                    return;

                var polygonMeshLinks = TopologyFunctions.ConnectOuterPolygonToMesh(polygonPositions, mesh);

                var fea = new ElementGroupGameObject()
                {
                    currentPolygon = polygon.polygon
                        .Zip(polygonPositions, (instanceId, pos) => new ElementGroupGameObject.PolygonElement() { instanceId = instanceId, positionsInRootRS = pos })
                        .ToList(),
                };


                foreach (var position in mesh.positions)
                {
                    fea.innerMeshElements.Add(position, CreateInnerMesh(position, spawnee, spawneeRotation, feaContainer.transform));
                }
                foreach (var connection in mesh.links)
                {
                    var fromPoint = mesh.positions[connection.Item1];
                    var toPoint = mesh.positions[connection.Item2];
                    fea.innerLinks.Add(new ElementGroupGameObject.InnerSpringJoint()
                    {
                        fromPoint = fromPoint,
                        toPoint = toPoint,
                        objs = (fea.innerMeshElements[fromPoint], fea.innerMeshElements[toPoint]),
                    });
                    this.connectionSpringDrawer.AddConnectionWithAnchor(
                        fea.innerMeshElements[fromPoint],
                        fea.innerMeshElements[toPoint],
                        fromPoint,
                        toPoint
                        );
                }
                foreach (var (polygonConnectionIndex, meshConnectionIndex) in polygonMeshLinks)
                {
                    var toPoint = mesh.positions[meshConnectionIndex];
                    fea.outerLinks.Add(new ElementGroupGameObject.OuterSpringJoint()
                    {
                        fromObject = polygonGameObjects[polygonConnectionIndex],
                        toPoint = mesh.positions[meshConnectionIndex],
                        objs = (polygonGameObjects[polygonConnectionIndex], fea.innerMeshElements[toPoint]),
                    });
                    this.connectionSpringDrawer.AddConnection(
                        polygonGameObjects[polygonConnectionIndex],
                        fea.innerMeshElements[toPoint]
                        );
                }

                polygon.getNonPolygonElements()
                    .ForEach(id =>
                    {
                        var go = gameObjectsMap[id];
                        go.SetActive(false);
                        fea.hiddenNodes.Add(new ElementGroupGameObject.HiddenNodes()
                        {
                            gameObject = go,
                            lastPosition = getGameObjectPosition(go),
                        });
                    });

                polygon.polygon
                    .Select(x => gameObjectsMap[x])
                    .ToList()
                    .ForEach(node => node.tag = Tags.FEM_EDGE_PARTICLE);

                FEAs.Add(fea);

                changed = true;
            }
        }

        FEAs.ForEach(fea => fea.lineDrawer.setPoints(fea.currentPolygon.Select(x => gameObjectsMap[x.instanceId])));
    }

    private static float GetForce(SpringJoint con_spring)
    {
        var baseObjectTransform = con_spring.gameObject.GetComponent<Transform>();
        var connObjectTransform = con_spring.connectedBody.gameObject.GetComponent<Transform>();
        float anchorLength = Vector3.Scale(con_spring.anchor - con_spring.connectedAnchor, baseObjectTransform.lossyScale).magnitude;
        float currentLength = (baseObjectTransform.position - connObjectTransform.position).magnitude;

        return (currentLength - anchorLength) * con_spring.spring;
    }

    public IEnumerable<(ElementGroupGameObject.InnerSpringJoint, float)> MonitorMesh(Dictionary<int, GameObject> gameObjectsMap)
    {
        return FEAs.Select(fea =>
        {
            var maxPull = float.MinValue;
            ElementGroupGameObject.InnerSpringJoint innerJoint = null;

            fea.innerLinks.ForEach(ij =>
            {
                var con_spring = connectionSpringDrawer.getSpringJoint(ij.objs.Item1, ij.objs.Item2);
                var force = GetForce(con_spring);
                var col = Color.green;
                // push 
                if (connectionSpringDrawer.getEliminated().Contains((ij.objs.Item1.GetInstanceID(), ij.objs.Item2.GetInstanceID())))
                {
                    col = Color.yellow;
                }
                else if (force < 0)
                {

                }
                // pull
                else
                {
                    float colorS = Mathf.Min(1, force / 1000);
                    col = Color.HSVToRGB(0, colorS, 1);

                    if (force > maxPull)
                    {
                        innerJoint = ij;
                        maxPull = force;
                    }

                    if (force > maxPull) maxPull = force;
                }
                connectionSpringDrawer.SetColor(ij.objs.Item1, ij.objs.Item2, col);
            });

            return (innerJoint, maxPull);
        });

    }

    // public void StartCrackPropagation(ElementGroupGameObject.InnerSpringJoint ij)
    // {
    //     if (SingleFEAData.status != ElementGroupGameObject.STATUS.CRACKING)
    //     {
    //         SingleFEAData.status = ElementGroupGameObject.STATUS.CRACKING;

    //         Debug.Log("StartCrackPropagation");

    //         connectionSpringDrawer.eliminateSpringJoint(ij.objs.Item1, ij.objs.Item2);
    //         //SingleFEAData.innerLinks.Remove(ij);
    //         //connectionSpringDrawer.RemoveConnection(ij.objs.Item1, ij.objs.Item2);
    //     }
    // }

    /* public void CalculateCrack((Tuple<float, float>, Tuple<float, float>) crackStart) // Dictionary<int, GameObject> gameObjectsMap
     {
         if (SingleFEAData != null)
         {
             var innerLinksDic = new Dictionary<(Tuple<float, float>, Tuple<float, float>), SpringJoint>();
             SingleFEAData.innerLinks.ForEach(ij =>
             {
                 var sj = connectionSpringDrawer.getSpringJoint(ij.objs.Item1, ij.objs.Item2);
                 innerLinksDic[(ij.fromPoint, ij.toPoint)] = sj;
                 innerLinksDic[(ij.toPoint, ij.fromPoint)] = sj;
             });

             var done = false;
             var (p1, p2) = crackStart;
             while (!done) 
             {
                 var p3 = Tuple.Create((float)((p1.Item1 + p2.Item1) / 2 - (p2.Item2 - p1.Item2) * Math.Sqrt(3) / 2), (float)((p1.Item2 + p2.Item2) / 2 + (p2.Item1 - p1.Item1) * Math.Sqrt(3) / 2));

                 if (!SingleFEAData.innerMeshElements.ContainsKey(p3))
                 {
                     done = true;
                 }
                 else
                 {
                     // var elem3 = SingleFEAData.innerMeshElements[p3];
                     var force13 = GetForce(innerLinksDic[(p1, p3)]);
                     var force12 = GetForce(innerLinksDic[(p1, p2)]);
                     if(force12 > force13)
                     {

                     }
                 }

             }
         }
     }*/


    private GameObject CreateInnerMesh(Tuple<float, float> position, GameObject spawnee, Quaternion spawneeRotation, Transform transform)
    {
        var go = Instantiate(spawnee, new Vector3(position.Item1, position.Item2, placementZ), spawneeRotation, transform);
        go.transform.localScale = new Vector3(0.2f, 1, 0.2f);

        if (!allowRotation)
        {
            var rb = go.GetComponent<Rigidbody>();
            // if(rb)
            rb.constraints = rb.constraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        go.tag = Tags.FEM_CENTER_PARTICLE;
        return go;
    }

    // Update is called once per frame
    void Update()
    {
        FEAs.ForEach(fea => fea.lineDrawer.updatePositions());
    }
};
