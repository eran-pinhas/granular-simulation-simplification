﻿using System.Collections;
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
            public ValueTuple<GameObject, GameObject> objs;
            public Tuple<float, float> fromPoint;
            public Tuple<float, float> toPoint;

            public float displacementRatio = 0;
            public float lastDeltaL = 0;
            public List<float> kComponents = new List<float>();

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
                //Debug.Log(displacementRatio);
            }


            public void UpdateK(ConnectionSpringDrawer drawer, float k)
            {
                var con_spring = drawer.getSpringJoint(objs.Item1, objs.Item2);
                con_spring.spring = k;
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

        public InnerSpringJoint crackStart;
        public InnerSpringJoint crackEnd;

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
    public bool CreateFeaObject(CycleFinder.ElementGroupPolygon polygon)
    {
        var spawneeRotation = pos.rotation;
        List<Particle> polygonGameObjects = polygon.polygon.Select(ind => childrenDict[ind]).ToList();
        List<Tuple<float, float>> polygonPositions = polygonGameObjects.Select(p => p.Position).ToList();

        var mesh = PolygonToMesh(polygonPositions, meshSize, bufferInside);

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
                tries++;

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

    private void EliminatedInnerJoint(ElementGroupGameObject.InnerSpringJoint ij)
    {
        connectionSpringDrawer.EliminateSpringJoint(ij.objs.Item1, ij.objs.Item2);
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


    private void MaintainCrack(ElementGroupGameObject fea)
    {
        var goOn = true;
        fea.innerLinks.ForEach(ij =>
        {
            var displacementRatio = ij.displacementRatio;
            var feaMaxForce = fea.status == ElementGroupGameObject.STATUS.FREE ? maxForce : propagateMaxForce;
            if (goOn && displacementRatio > feaMaxForce && !IsInnerJoinEliminated(ij))
            {
                if (fea.status != ElementGroupGameObject.STATUS.CRACKING)
                {
                    fea.status = ElementGroupGameObject.STATUS.CRACKING;

                    Debug.Log(String.Format("StartCrackPropagation {0}", displacementRatio));
                    fea.crackStart = ij;
                    fea.crackEnd = ij;

                    EliminatedInnerJoint(ij);
                    fea.lineDrawer.setColor(Color.red);
                    //SingleFEAData.innerLinks.Remove(ij);
                    //connectionSpringDrawer.RemoveConnection(ij.objs.Item1, ij.objs.Item2);
                }
                else
                {
                    var crackContinued = false;
                    if (ShouldContinuePropogate(fea.crackEnd, ij, fea))
                    {
                        fea.crackEnd = ij;
                        EliminatedInnerJoint(ij);
                        crackContinued = true;
                    }
                    else if (ShouldContinuePropogate(fea.crackStart, ij, fea))
                    {
                        fea.crackStart = ij;
                        EliminatedInnerJoint(ij);
                        crackContinued = true;
                    }
                    else
                    {
                        Debug.Log("propogate not related");
                    }

                    if (crackContinued)
                    {
                        goOn = false;
                        if (fea.crackStart != fea.crackEnd && fea.isEgdeInnerJoint(fea.crackStart) && fea.isEgdeInnerJoint(fea.crackEnd)
                         || fea.crackStart == fea.crackEnd && fea.isDoubleEgdeInnerJoint(fea.crackStart))
                        {
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
    }

    private static void SplitFea(ElementGroupGameObject fea)
    {
        new ElementGroupGameObject();
    }

    private bool ShouldContinuePropogate(ElementGroupGameObject.InnerSpringJoint current, ElementGroupGameObject.InnerSpringJoint newIJ, ElementGroupGameObject fea)
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
                return true;
            }
        }
        return false;
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
                il.UpdateK(connectionSpringDrawer, (float)boundIlK);
            });
        });


        ColorizeMesh();
        var maxMaxPull = FEAs.Select(fea => fea.innerLinks.Select(ij => ij.displacementRatio).Max()).DefaultIfEmpty(-1).Max();
        maxMaxPull = ((maxMaxPull - 1) * 100);
        reporter.reportNew(maxMaxPull > float.MinValue ? maxMaxPull : -1, Time.time);

        FEAs.ForEach(fea =>
        {
            MaintainCrack(fea);
        });

        var cycles = CycleFinder.Find<bool>(childrenDict.Select(t => t.Key), collisions, 3);

        var adj = CycleFinder.FindAdjacantCicles(cycles, nodeId => childrenDict[nodeId].Type == Particle.PARTICLE_TYPE.FEM_EDGE_PARTICLE, instanceId => childrenDict[instanceId].Position);
        MaintainFea(adj);
    }
};
