using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;


public class MeshGenerator : MonoBehaviour, ICollisionListener
{
    public class ElementGroupGameObject
    {
        public class HiddenNodes
        {
            public Particle particle;
            public Tuple<float, float> lastPosition;
        }

        public class InnerSpringJoint
        {

            public InnerSpringJoint(Tuple<float, float> _fromPoint,
                Tuple<float, float> _toPoint,
                ValueTuple<GameObject, GameObject> _objs,
                ConnectionSpringDrawer _csd
            )
            {
                fromPoint = _fromPoint;
                toPoint = _toPoint;
                objs = _objs;
                csd = _csd;

                csd.AddConnectionWithAnchor(objs.Item1, objs.Item2, TopologyFunctions.Substract(toPoint, fromPoint));
            }

            public ValueTuple<GameObject, GameObject> objs;
            public Tuple<float, float> fromPoint;
            public Tuple<float, float> toPoint;
            private ConnectionSpringDrawer csd;
            private bool eliminated;

            public float displacementRatio = 0;
            public float lastDeltaL = 0;
            public List<float> kComponents = new List<float>();

            public void ExpandJoint()
            {
                Debug.Assert(!eliminated);
                csd.RemoveConnection(objs.Item1, objs.Item2);
                csd.AddConnectionWithAnchor(objs.Item1, objs.Item2, TopologyFunctions.Scale(TopologyFunctions.Substract(toPoint, fromPoint), 1.1f));
                eliminated = true;
            }

            public void InitForceAggregation()
            {
                this.kComponents.Clear();
            }

            public void Calcforce(ConnectionSpringDrawer drawer, float adaptation)
            {
                var con_spring = drawer.getSpringJoint(objs.Item1, objs.Item2);
                var baseObjectTransform = con_spring.gameObject.GetComponent<Transform>();
                var connObjectTransform = con_spring.connectedBody.gameObject.GetComponent<Transform>();
                float anchorLength = Vector3.Scale(con_spring.anchor - con_spring.connectedAnchor, baseObjectTransform.lossyScale).magnitude;
                float currentLength = (baseObjectTransform.position - connObjectTransform.position).magnitude;

                this.lastDeltaL = currentLength - anchorLength;

                var messure = currentLength / anchorLength;
                displacementRatio = messure * adaptation + displacementRatio * (1 - adaptation);
            }


            public void UpdateK(ConnectionSpringDrawer drawer, float k)
            {
                var con_spring = drawer.getSpringJoint(objs.Item1, objs.Item2);
                con_spring.spring = k;
            }

            internal void EliminateJoint(ConnectionSpringDrawer connectionSpringDrawer)
            {
                connectionSpringDrawer.EliminateSpringJoint(objs.Item1, objs.Item2);
                this.ExpandJoint();
            }
        }
        public class OuterSpringJoint
        {
            public ValueTuple<GameObject, GameObject> objs;
            public Particle particle;
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

        public List<InnerSpringJoint> crackItems = new List<InnerSpringJoint>();

        public List<HiddenNodes> hiddenNodes = new List<HiddenNodes>();
        public List<OuterSpringJoint> outerLinks = new List<OuterSpringJoint>();
        public List<InnerSpringJoint> innerLinks = new List<InnerSpringJoint>();
        public Dictionary<Tuple<float, float>, GameObject> innerMeshElements = new Dictionary<Tuple<float, float>, GameObject>();
        public List<PolygonElement> currentPolygon;
        public STATUS status = STATUS.FREE;
        public LineDrawer lineDrawer;

        public Color color;
        public int id;

        static int currentId = 1;

        public ElementGroupGameObject()
        {
            color = Color.HSVToRGB(UnityEngine.Random.value, 0.73f, 0.96f);
            lineDrawer = new LineDrawer(new Vector3(0, 0, -10), color);
            id = currentId;
            currentId++;
        }

        public void Complete()
        {
            Destroy(lineDrawer.line);
        }

        private int getEgdeCount(InnerSpringJoint ij)
        {
            // adding (pos1,pos2) and (pos2,pos1) to the hashset
            var innerJointsPositions = innerLinks.Select(x => (x.fromPoint, x.toPoint)).Concat(innerLinks.Select(x => (x.toPoint, x.fromPoint)));
            var linksPositions = new HashSet<(Tuple<float, float>, Tuple<float, float>)>(innerJointsPositions);

            var pos1 = ij.fromPoint;
            var pos2 = ij.toPoint;
            return innerMeshElements
                // Iterating threw all inner nodes except this ones inner
                .Select(innerMeshElement => innerMeshElement.Key)
                .Except(new List<Tuple<float, float>>() { pos1, pos2 })
                // looking for nodes that are linked to both pos1 and pos2
                .Where(innerPos => linksPositions.Contains((pos1, innerPos)) && linksPositions.Contains((pos2, innerPos)))
                // if there are less than 2 - this is an egde
                .Count();
        }

        public bool isEgdeInnerJoint(InnerSpringJoint ij)
        {
            return getEgdeCount(ij) == 1;
        }

        public bool isDoubleEgdeInnerJoint(InnerSpringJoint ij)
        {
            return getEgdeCount(ij) == 0;
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
    public float maxForce = 0.1f;
    public float propagateMaxForce = 0.05f;
    public float adaptation = 0.2f;
    public float springK = 10000;
    public Reporter reporter;
    public GameObject spawnee;
    public Transform pos;
    public ConnectionDrawer connectionDrawer;

    public Dictionary<int, Dictionary<int, bool>> collisions = new Dictionary<int, Dictionary<int, bool>>();

    private List<Particle> children = new List<Particle>();
    private Dictionary<int, Particle> childrenDict = new Dictionary<int, Particle>();
    private GameObject feaContainer;

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

    void Start()
    {
        feaContainer = new GameObject("FEA_data");
    }

    private List<ElementGroupGameObject> FEAs = new List<ElementGroupGameObject>();

    private (Tuple<float, float>, Tuple<float, float>) StringifyNodesLink(Tuple<float, float> pos1, Tuple<float, float> pos2)
    {
        return (pos1.Item1 < pos2.Item1 || (pos1.Item1 == pos2.Item1 && pos1.Item2 < pos2.Item2)) ? (pos1, pos2) : (pos2, pos1);
    }

    public bool CreateFeaObject(CycleFinder.ElementGroupPolygon polygon, bool v = false)
    {
        var spawneeRotation = pos.rotation;
        List<Particle> polygonGameObjects = polygon.polygon.Select(ind => childrenDict[ind]).ToList();
        List<Tuple<float, float>> polygonPositions = polygonGameObjects.Select(p => p.Position).ToList();

        var mesh = PolygonToMesh(polygonPositions, meshSize, bufferInside);

        if (v)
        {
            Debug.Log("-----------VERBOSE-------");
            Debug.Log(string.Format("{0}, {1}", polygon.polygon.Count, minStringSize));
            Debug.Log(string.Format("{0}, {1}", polygon.restElements.Count * 1.5, polygon.polygon.Count));
            Debug.Log(string.Format("{0}, {1}", TopologyFunctions.PolsbyPopper(polygonPositions), PPTestMin));
            Debug.Log("-------------------------");
        }
        if (polygon.polygon.Count < minStringSize)
            return false;
        if (polygon.restElements.Count * 1.5 < polygon.polygon.Count)
            return false;
        if (TopologyFunctions.PolsbyPopper(polygonPositions) < PPTestMin)
            return false;

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
            fea.innerLinks.Add(new ElementGroupGameObject.InnerSpringJoint(
                 fromPoint,
                 toPoint,
                 (fea.innerMeshElements[fromPoint], fea.innerMeshElements[toPoint]),
                 this.connectionSpringDrawer)
                );
        }
        foreach (var (polygonConnectionIndex, meshConnectionIndex) in polygonMeshLinks.Distinct())
        {
            var toPoint = mesh.positions[meshConnectionIndex];
            fea.outerLinks.Add(new ElementGroupGameObject.OuterSpringJoint()
            {
                particle = polygonGameObjects[polygonConnectionIndex],
                toPoint = toPoint,
                objs = (polygonGameObjects[polygonConnectionIndex].gameObject, fea.innerMeshElements[toPoint]),
            });
            this.connectionSpringDrawer.AddConnection(
                polygonGameObjects[polygonConnectionIndex].gameObject,
                fea.innerMeshElements[toPoint]
                );
        }

        polygon.getNonPolygonElements()
            .ForEach(id =>
            {
                var p = childrenDict[id];
                p.setAsHidden(fea.id);
                fea.hiddenNodes.Add(new ElementGroupGameObject.HiddenNodes()
                {
                    particle = p,
                    lastPosition = p.Position,
                });
            });

        polygon.polygon
            .Select(x => childrenDict[x])
            .ToList()
            .ForEach(node => node.setSetAsEdge(fea.id));

        FEAs.Add(fea);
        return true;
    }
    public void MaintainFea(List<CycleFinder.ElementGroupPolygon> groupPolygons)
    {
        var spawneeRotation = pos.rotation;
        var changed = false;

        // Maintain existing FEAs

        FEAs.ForEach(fea =>
        {
            if (fea.status == ElementGroupGameObject.STATUS.CRACKING)
                return;


            // filtering out any polygons that are touching FEAs which are not this
            var touchingPolygons = groupPolygons
                .Where(group => group.isTouchingExistingFEM)
                .Where(group => !group.polygon
                    .Select(p_id => childrenDict[p_id])
                    .Any(p => p.Type == Particle.PARTICLE_TYPE.FEM_EDGE_PARTICLE && p.particleGroupId != fea.id));

            foreach (var touchingPolygon in touchingPolygons)
            {
                var hasOtherFeaParticles = touchingPolygon.polygon
                    .Concat(touchingPolygon.restElements)
                    .Select(i => childrenDict[i])
                    .Any(p => p.particleGroupId > 0 && p.particleGroupId != fea.id);

                if (hasOtherFeaParticles || changed || touchingPolygon.sourceCycles.Count < 5)
                    continue;

                var feaPolygonIds = new HashSet<int>(fea.currentPolygon.Select(p => p.instanceId));
                var feaPolygonPositions = fea.currentPolygon.Select(p => childrenDict[p.instanceId].Position).ToList();

                var unifiedCycles = touchingPolygon.sourceCycles
                    // Removing particles stuck in the inside
                    .Where(cycle => cycle.All(p => feaPolygonIds.Contains(p) || !TopologyFunctions.PointInPolygon(feaPolygonPositions, childrenDict[p].Position, 0)))
                    // Adding the current polygon itself
                    .Concat(new List<List<int>>() {
                        fea.currentPolygon.GetRange(0, fea.currentPolygon.Count - 1).Select(x=>x.instanceId).ToList()
                    })
                    .ToList();

                var unifiedPolygons = CycleFinder.FindAdjacantCicles(unifiedCycles, x => false, instanceId => childrenDict[instanceId].Position);



                if (unifiedPolygons.Count != 1)
                {
                    continue;
                }

                var unifiedPolygon = unifiedPolygons[0];

                var polygonGameObjects = unifiedPolygon.polygon.Select(ind => childrenDict[ind]).ToList();
                var newPolygonPositions = polygonGameObjects.Select(p => p.Position).ToList();

                if (TopologyFunctions.PolsbyPopper(newPolygonPositions) < PPTestMin)
                    continue;

                var polygonLastPositions = fea.currentPolygon.Select(x => x.positionsInRootRS);
                var polygonCurrentPositions = fea.currentPolygon.Select(node => childrenDict[node.instanceId].Position);

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
                var restElements_remove = unifiedPolygon.getNonPolygonElements().Except(currentPolygonIds); // Not sure about that

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
                var stringifiedExistingOuterLinks = fea.outerLinks.Select(x => (x.particle.Id, x.toPoint));
                var stringifiedNewOuterLinks = polygonMeshLinks.Select(link => (polygonGameObjects[link.Item1].Id, mesh_t.positions[link.Item2]));
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
                    var polygonParticle = childrenDict[polygonElemId];
                    var linkToRemove = fea.outerLinks.First(j => j.particle.Id == polygonParticle.Id && j.toPoint == meshElemPos);

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
                foreach (var p in polygonNode_remove.Select(nodeId => childrenDict[nodeId]))
                {
                    p.setAsHidden(fea.id);
                    fea.hiddenNodes.Add(new ElementGroupGameObject.HiddenNodes()
                    {
                        particle = p,
                        lastPosition = TopologyFunctions.TranformPoint(tranformMatrixInv, p.Position),
                    });
                }
                foreach (var instanceId in restElements_remove)
                {
                    var p = childrenDict[instanceId];
                    p.setAsHidden(fea.id);
                    fea.hiddenNodes.Add(new ElementGroupGameObject.HiddenNodes()
                    {
                        particle = p,
                        lastPosition = TopologyFunctions.TranformPoint(tranformMatrixInv, p.Position),
                    });
                }

                // (1)
                foreach (var nodeId in polygonNode_add)
                {
                    var p = childrenDict[nodeId];
                    p.setSetAsEdge(fea.id);

                    Tuple<float, float> pos = null;
                    fea.hiddenNodes.Where(n => n.particle == p).ToList().ForEach(hiddenNode =>
                    {
                        p.Position = TopologyFunctions.TranformPoint(tranformMatrix, hiddenNode.lastPosition);
                        pos = hiddenNode.lastPosition;
                        fea.hiddenNodes.Remove(hiddenNode);
                        p.setSetAsEdge(fea.id);
                    });

                    if (pos == null)
                    {
                        pos = TopologyFunctions.TranformPoint(tranformMatrixInv, p.Position);
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
                // (3)
                foreach (var (p1, p2) in innerLinks_add)
                {
                    var p1GO = fea.innerMeshElements[p1];
                    var p2GO = fea.innerMeshElements[p2];

                    fea.innerLinks.Add(new ElementGroupGameObject.InnerSpringJoint(p1, p2, (p1GO, p2GO), this.connectionSpringDrawer));
                }
                // (4)
                foreach (var (goId, meshElemPos) in outerLinks_add)
                {
                    var meshElemGO = fea.innerMeshElements[meshElemPos];
                    var polygonParticle = childrenDict[goId];

                    var polyGO = polygonParticle.gameObject;
                    fea.outerLinks.Add(new ElementGroupGameObject.OuterSpringJoint
                    {
                        particle = polygonParticle,
                        toPoint = meshElemPos,
                        objs = (polyGO, meshElemGO),
                    });
                    this.connectionSpringDrawer.AddConnection(polyGO, meshElemGO);
                }
                // Debug.Log(string.Format("DONE {0},{1}", polygonNode_add.Count(), polygonNode_remove.Count()));
            }

        });

        foreach (var polygon in groupPolygons)
            if (!changed)
                changed = CreateFeaObject(polygon);


        FEAs.ForEach(fea => fea.lineDrawer.setPoints(fea.currentPolygon.Select(x => childrenDict[x.instanceId])));
    }

    private bool IsInnerJoinEliminated(ElementGroupGameObject.InnerSpringJoint ij)
    {
        return connectionSpringDrawer.getEliminated().Contains((ij.objs.Item1.GetInstanceID(), ij.objs.Item2.GetInstanceID()));
    }

    private void EliminateInnerJoint(ElementGroupGameObject.InnerSpringJoint ij)
    {
        ij.EliminateJoint(connectionSpringDrawer);
    }

    public void ColorizeMesh()
    {
        FEAs.ForEach(fea =>
        {
            fea.innerLinks.ForEach(ij =>
            {
                var displacementRatio = ij.displacementRatio;
                Color col = Color.white;
                if (IsInnerJoinEliminated(ij))
                {
                    col = Color.black;
                }
                // push 
                else if (displacementRatio < 1)
                {
                    col = Color.green;
                }
                // pull
                else
                {
                    float colorS = Mathf.Min(1, (displacementRatio - 1) * 10);
                    col = Color.HSVToRGB(0, colorS, 1);
                }
                connectionSpringDrawer.SetColor(ij.objs.Item1, ij.objs.Item2, col);
            });
        });

    }

    private static bool isEqualOrSwitched<T>((T, T) a, (T, T) b)
    {
        return a.Item1.Equals(b.Item1) && a.Item2.Equals(b.Item2) || a.Item1.Equals(b.Item2) && a.Item2.Equals(b.Item1);
    }


    private bool MaintainCrack(ElementGroupGameObject fea)
    {
        var hadChange = false;
        fea.innerLinks.ForEach(ij =>
        {
            var displacementRatio = ij.displacementRatio;
            var feaMaxForce = fea.status == ElementGroupGameObject.STATUS.FREE ? maxForce : propagateMaxForce;
            if (!hadChange && displacementRatio > feaMaxForce && !IsInnerJoinEliminated(ij))
            {
                if (fea.status != ElementGroupGameObject.STATUS.CRACKING)
                {
                    fea.status = ElementGroupGameObject.STATUS.CRACKING;

                    Debug.Log(String.Format("StartCrackPropagation {0}", displacementRatio));

                    Debug.Assert(!fea.crackItems.Any());
                    fea.crackItems.Add(ij);

                    EliminateInnerJoint(ij);
                    fea.lineDrawer.setColor(Color.red);
                    //SingleFEAData.innerLinks.Remove(ij);
                    //connectionSpringDrawer.RemoveConnection(ij.objs.Item1, ij.objs.Item2);
                }
                else
                {
                    var crackContinued = false;
                    if (ShouldContinuePropogate(fea.crackItems.Last(), ij, fea).Item1)
                    {
                        fea.crackItems.Add(ij);
                        EliminateInnerJoint(ij);
                        crackContinued = true;
                    }
                    else if (ShouldContinuePropogate(fea.crackItems.First(), ij, fea).Item1)
                    {

                        fea.crackItems.Insert(0, ij);
                        EliminateInnerJoint(ij);
                        crackContinued = true;
                    }
                    else
                    {
                        //Debug.Log("propogate not related");
                    }

                    if (crackContinued)
                    {

                        var crackEnd = fea.crackItems.Last();
                        var crackStart = fea.crackItems.First();
                        hadChange = true;
                        if (crackStart != crackEnd && fea.isEgdeInnerJoint(crackStart) && fea.isEgdeInnerJoint(crackEnd)
                         || crackStart == crackEnd && fea.isDoubleEgdeInnerJoint(crackStart))
                        {
                            SplitFea(fea);
                            Debug.Log("Done propagating");
                        }
                        else
                        {
                            Debug.Log("continue propegating");
                        }
                    }

                }
            }
        });
        return hadChange;
    }

    private int getClosestPoligonEdge(ElementGroupGameObject.InnerSpringJoint ij, Tuple<float, float> pointBehind, List<Tuple<float, float>> polygon)
    {
        var center = TopologyFunctions.CenterOf(ij.fromPoint, ij.toPoint);
        var extreme = new Tuple<float, float>(center.Item1 + 1000 * (-1) * (pointBehind.Item1 - center.Item1), center.Item2 + 1000 * (-1) * (pointBehind.Item2 - center.Item2));
        var intersectingIndices = polygon.Select((point, i) =>
        {
            if (i == 0) return -1;
            else
            {
                var p1 = polygon[i - 1];
                var p2 = point;
                if (TopologyFunctions.DoIntersect2(p1, p2, center, extreme))
                    return i - 1;
                else
                    return -1;
            }
        }).Where(i => i != -1);

        var closestIndex = intersectingIndices.Aggregate((i1, i2) =>
            TopologyFunctions.Distance(center, TopologyFunctions.CenterOf(polygon[i1], polygon[i1 + 1])) <
                TopologyFunctions.Distance(center, TopologyFunctions.CenterOf(polygon[i2], polygon[i2 + 1])) ?
            i1 :
            i2);
        return closestIndex;

    }


    private void DismantleFEA(ElementGroupGameObject fea)
    {
        var polygonLastPositions = fea.currentPolygon.Select(x => x.positionsInRootRS);
        var polygonCurrentPositions = fea.currentPolygon.Select(node => childrenDict[node.instanceId].Position);
        var tranformMatrix = TopologyFunctions.LinearRegression2d(polygonLastPositions, polygonCurrentPositions);



        foreach (var il in fea.innerLinks)
            this.connectionSpringDrawer.RemoveConnection(il.objs.Item1, il.objs.Item2);

        foreach (var ol in fea.outerLinks)
            this.connectionSpringDrawer.RemoveConnection(ol.objs.Item1, ol.objs.Item2);

        foreach (var inner in fea.innerMeshElements)
            Destroy(inner.Value);

        foreach (var hiddenNode in fea.hiddenNodes)
        {
            hiddenNode.particle.setAsFree();
            var newPos = TopologyFunctions.TranformPoint(tranformMatrix, hiddenNode.lastPosition);
            hiddenNode.particle.Position = newPos;
        }

        Debug.Log(string.Format("------- DISMANTLE STATS ----------"));
        Debug.Log(string.Format("hidden {0}", fea.hiddenNodes.Count));
        Debug.Log(string.Format("outerLinks {0}", fea.outerLinks.Count));
        Debug.Log(string.Format("innerLinks {0}", fea.innerLinks.Count));
        Debug.Log(string.Format("----------------------------------"));
    }

    private void SplitFea(ElementGroupGameObject fea)
    {
        fea.Complete();
        this.FEAs.Remove(fea);

        var poly = fea.currentPolygon;
        var n = poly.Count;

        var points = poly.Select(s => s.positionsInRootRS).ToList();
        var firstCrackItem = fea.crackItems.First();
        var lastCrackItem = fea.crackItems.Last();
        var (headShouldBeFalse, head_backDirection) = ShouldContinuePropogate(fea.crackItems[1], firstCrackItem, fea);
        var (tailShouldBeFalse, tail_backDirection) = ShouldContinuePropogate(fea.crackItems[fea.crackItems.Count() - 2], lastCrackItem, fea);

        var headIndex = getClosestPoligonEdge(firstCrackItem, head_backDirection, points);
        var tailIndex = getClosestPoligonEdge(lastCrackItem, tail_backDirection, points);

        var headToTailIndicesRange = ((headIndex + 1) % n, tailIndex);
        var tailToHeadIndicesRange = ((tailIndex + 1) % n, headIndex);

        var crackHeadPositionOnPolygon = TopologyFunctions.CenterOf(poly[tailToHeadIndicesRange.Item2].positionsInRootRS, poly[headToTailIndicesRange.Item1].positionsInRootRS);
        var crackTailPositionOnPolygon = TopologyFunctions.CenterOf(poly[headToTailIndicesRange.Item2].positionsInRootRS, poly[tailToHeadIndicesRange.Item1].positionsInRootRS);

        var headToTailSeamPositions = fea.crackItems.Select(s => TopologyFunctions.CenterOf(s.fromPoint, s.toPoint)).ToList();
        headToTailSeamPositions.Insert(0, crackHeadPositionOnPolygon);
        headToTailSeamPositions.Add(crackTailPositionOnPolygon);
        //var extendedPolygonNodeInEnd_inRS = TopologyFunctions.CenterOf(pointAfter.positionsInRootRS, poly[range.Item2].positionsInRootRS);

        var tailToHeadSeamPositions = new List<Tuple<float, float>>(headToTailSeamPositions);
        tailToHeadSeamPositions.Reverse();

        // IMPORTANT exectute before dismantling
        var egp1 = HandleGroupAssemply(fea, headToTailIndicesRange, tailToHeadSeamPositions);
        var egp2 = HandleGroupAssemply(fea, tailToHeadIndicesRange, headToTailSeamPositions);

        DismantleFEA(fea);

        CreateFeaObject(egp1, true);
        CreateFeaObject(egp2, true);
    }


    private CycleFinder.ElementGroupPolygon HandleGroupAssemply(
        ElementGroupGameObject fea,
        (int, int) range,
        List<Tuple<float, float>> seamEndToStart_inRS
    )
    {
        var n = fea.currentPolygon.Count;
        //var extendedPolygonNodeInStart_inRS = TopologyFunctions.CenterOf(pointBefore.positionsInRootRS, fea.currentPolygon[range.Item1].positionsInRootRS);
        //var extendedPolygonNodeInEnd_inRS = TopologyFunctions.CenterOf(pointAfter.positionsInRootRS, fea.currentPolygon[range.Item2].positionsInRootRS);
        var elementsString = range.Item1 < range.Item2 ?
            fea.currentPolygon.GetRange(range.Item1, range.Item2 - range.Item1 + 1) :
            fea.currentPolygon.GetRange(range.Item1, n - range.Item1).Concat(fea.currentPolygon.GetRange(0, range.Item2 + 1)).ToList();

        // polygon + seam + polygon[0]
        var extendedPolygon_inRS = new List<Tuple<float, float>>(elementsString.Select(s => s.positionsInRootRS));
        extendedPolygon_inRS.AddRange(seamEndToStart_inRS);
        extendedPolygon_inRS.Add(extendedPolygon_inRS.First());


        var elementsInside = fea.hiddenNodes.Where(p => TopologyFunctions.PointInPolygon(extendedPolygon_inRS, p.lastPosition, 0f));
        ;
        var newEdgeElements = elementsInside.Where(particleInside => seamEndToStart_inRS.Where((p2, i) =>
        {
            if (i == 0) return false;
            var p1 = seamEndToStart_inRS[i - 1];
            return TopologyFunctions.Distance(p1, particleInside.lastPosition) < 3.5 &&
                TopologyFunctions.Distance(p1, particleInside.lastPosition) < 3.5 &&
                TopologyFunctions.DistanceLineToPoint(p1, p2, particleInside.lastPosition) < 0.4;
        }).Count() > 0
            )
            .ToList();

        // TODO RMEOVE
        var last = seamEndToStart_inRS.Last();
        newEdgeElements.Sort((p1, p2) => TopologyFunctions.Distance(p1.lastPosition, last) < TopologyFunctions.Distance(p2.lastPosition, last) ? -1 : 1);

        // first and last should be the same
        var allPolygon = new List<ElementGroupGameObject.PolygonElement>(elementsString);
        allPolygon.AddRange(newEdgeElements.Select(ee => new ElementGroupGameObject.PolygonElement()
        {
            instanceId = ee.particle.Id,
            positionsInRootRS = ee.lastPosition
        }));
        allPolygon.Add(elementsString.First());

        var elementsInsideWithoutNewEdge = elementsInside.Where(p => !newEdgeElements.Contains(p));

        // isTouchingExistingFEM & sourceCycles are made up - they are not required for CreateFeaObject execution
        return new CycleFinder.ElementGroupPolygon()
        {
            polygon = allPolygon.Select(s => s.instanceId).ToList(),
            holes = new List<List<int>>(),
            restElements = elementsInsideWithoutNewEdge.Select(s => s.particle.Id).ToList(),
            isTouchingExistingFEM = false,
            sourceCycles = null,
        };
    }

    private (bool, Tuple<float, float>) ShouldContinuePropogate(ElementGroupGameObject.InnerSpringJoint current, ElementGroupGameObject.InnerSpringJoint newIJ, ElementGroupGameObject fea)
    {
        var (cur1, cur2) = (current.fromPoint, current.toPoint);
        var (new1, new2) = (newIJ.fromPoint, newIJ.toPoint);

        var items = new HashSet<Tuple<float, float>>() { cur1, cur2, new1, new2 };
        if (items.Count <= 2)
        {
            Debug.Log("ISSUE - same item found - should have been eliminated");
        }
        else if (items.Count == 3)
        {
            var newOne = items.Except(new HashSet<Tuple<float, float>>() { cur1, cur2 }).Single();
            var endOne = items.Except(new HashSet<Tuple<float, float>>() { new1, new2 }).Single();
            var mutualOne = items.Except(new HashSet<Tuple<float, float>>() { newOne, endOne }).Single();

            // if propagates from connection 1-2 to 2-3 we would like to have the 1-3 connection
            var nextIsConnectedToPrevious = fea.innerLinks.Any(ij => isEqualOrSwitched((ij.fromPoint, ij.toPoint), (newOne, endOne)) && !IsInnerJoinEliminated(ij));
            if (nextIsConnectedToPrevious)
            {
                var itemsWithoutCurrent = new HashSet<Tuple<float, float>>(items);
                itemsWithoutCurrent.Remove(new1);
                itemsWithoutCurrent.Remove(new2);

                return (true, itemsWithoutCurrent.Single());

            }
        }
        return (false, null);
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

        return go;
    }

    public void informNewChild(Particle obj)
    {
        children.Add(obj);
        childrenDict.Add(obj.Id, obj);
        collisions.Add(obj.Id, new Dictionary<int, bool>());
    }

    public void informCollision(Particle a, Particle b)
    {
        collisions[a.Id].Add(b.Id, true); // , line
        this.connectionDrawer.AddConnection(a.gameObject, b.gameObject);
    }

    public void informCollisionRemoved(Particle a, Particle b)
    {
        this.connectionDrawer.RemoveConnection(a.gameObject, b.gameObject);
        collisions[a.Id].Remove(b.Id);
    }

    private class Cycle
    {
        public readonly Tuple<float, float> elem1, elem2, elem3;
        public Cycle(Tuple<float, float> elem1, Tuple<float, float> elem2, Tuple<float, float> elem3)
        {
            var a = new List<Tuple<float, float>>() { elem1, elem2, elem3 };
            a.Sort((emp1, emp2) => LT(emp1, emp2) ? 1 : -1);

            this.elem1 = a[0];
            this.elem2 = a[1];
            this.elem3 = a[2];
        }

        private bool LT(Tuple<float, float> a, Tuple<float, float> b)
        {
            return a.Item1 > b.Item1 || (a.Item1 == b.Item1 && a.Item2 > b.Item2);
        }
        public bool IsValid()
        {
            return !(this.elem1 == this.elem2 || this.elem3 == this.elem2 || this.elem1 == this.elem3);
        }
        public override bool Equals(object o)
        {
            Cycle c1 = o as Cycle;
            return c1 != null && c1.elem1 == elem1 && c1.elem2 == elem2 && c1.elem3 == elem3;
        }
        public override int GetHashCode()
        {
            return new Tuple<Tuple<float, float>, Tuple<float, float>, Tuple<float, float>>(elem1, elem2, elem3).GetHashCode();
        }
    }

    private IEnumerable<Cycle> GetInnerLinksCircles(ElementGroupGameObject fea)
    {
        var allConnections = new HashSet<(Tuple<float, float>, Tuple<float, float>)>(fea.innerLinks.Select(s => (s.fromPoint, s.toPoint)).Concat(fea.innerLinks.Select(s => (s.toPoint, s.fromPoint))));
        var allPotentialTriangles = new HashSet<Cycle>(fea.innerLinks.SelectMany(il => fea.innerMeshElements.Select(ime =>
           new Cycle(il.fromPoint, il.toPoint, ime.Key)
       )));

        var allTriangles = allPotentialTriangles.Where(c => c.IsValid() &&
            allConnections.Contains((c.elem1, c.elem2)) &&
            allConnections.Contains((c.elem3, c.elem2)) &&
            allConnections.Contains((c.elem1, c.elem3)));
        return allTriangles;
    }

    void Update()
    {
        FEAs.ForEach(fea =>
        {
            fea.lineDrawer.updatePositions();
            fea.innerLinks.ForEach(ij => ij.Calcforce(connectionSpringDrawer, adaptation));
            fea.innerLinks.ForEach(ij => ij.InitForceAggregation());

            Dictionary<(Tuple<float, float>, Tuple<float, float>), int> connections2InnerLinksIndex = fea.innerLinks.Select((s, i) => new KeyValuePair<(Tuple<float, float>, Tuple<float, float>), int>((s.fromPoint, s.toPoint), i)).ToDictionary(s => s.Key, s => s.Value);

            GetInnerLinksCircles(fea).ToList().ForEach(cycle =>
            {
                var connections = new List<(Tuple<float, float>, Tuple<float, float>)>() { (cycle.elem1, cycle.elem2), (cycle.elem3, cycle.elem2), (cycle.elem1, cycle.elem3) };
                var cycleInnerLinks = connections.Select(
                    (k, v) => connections2InnerLinksIndex.ContainsKey((k.Item1, k.Item2)) ?
                        connections2InnerLinksIndex[(k.Item1, k.Item2)] :
                        connections2InnerLinksIndex[(k.Item2, k.Item1)]
                    ).Select(i => fea.innerLinks[i]).ToList();
                var cycleAverageDisplacement = cycleInnerLinks.Select(il => il.lastDeltaL).Average();
                cycleInnerLinks.ForEach(s => s.kComponents.Add(springK * cycleAverageDisplacement / s.lastDeltaL));
            });

            fea.innerLinks.ForEach(il =>
            {
                il.kComponents.Add(springK);
                var ilK = il.kComponents.Average();

                var boundIlK = Math.Max(Math.Min(ilK, 1.2 * springK), 0.8 * springK);
                float finalK = IsInnerJoinEliminated(il) ? springK : (float)boundIlK;
                il.UpdateK(connectionSpringDrawer, finalK);
            });
        });


        ColorizeMesh();
        var maxMaxPull = FEAs.Select(fea => fea.innerLinks.Select(ij => ij.displacementRatio).Max()).DefaultIfEmpty(-1).Max();
        maxMaxPull = ((maxMaxPull - 1) * 100);
        reporter.reportNew(maxMaxPull > float.MinValue ? maxMaxPull : -1, Time.time);

        // In case FEAs changes in the process
        var feaClone = new List<ElementGroupGameObject>(FEAs);
        foreach (var fea in FEAs)
        {
            var changed = MaintainCrack(fea);
            if (changed)
                break;
        }

        var cycles = CycleFinder.Find(childrenDict.Select(t => t.Key), collisions, 3);

        var adj = CycleFinder.FindAdjacantCicles(cycles, nodeId => childrenDict[nodeId].Type == Particle.PARTICLE_TYPE.FEM_EDGE_PARTICLE, instanceId => childrenDict[instanceId].Position);
        MaintainFea(adj);
    }
};
