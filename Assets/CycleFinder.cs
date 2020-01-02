using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System;

struct PotentialLink
{
    int TTL;
    int nodeId;
}
public class CycleFinder
{


    public class ElementGroupPolygon
    {
        public List<int> polygon;
        public List<List<int>> holes;
        public List<int> restElements;
        public bool isTouchingExistingFEM;
        public List<List<int>> sourceCycles;

        internal List<int> getNonPolygonElements()
        {
            return holes
                        .SelectMany(x => x)
                        .Concat(restElements)
                        .ToList();
        }
    }

    const int NON_VISITED = 0;
    const int VISITED = 1;
    const int DONE = 2;

    static int mod(int x, int m)
    {
        int r = x % m;
        return r < 0 ? r + m : r;
    }
    static double mod(double x, double m)
    {
        double r = x % m;
        return r < 0 ? r + m : r;
    }

    public static Stopwatch stopWatch = new Stopwatch();

    static bool singularConstraint(int a, int b, int c)
    {
        // in 3 cycle - ordered a<b<c
        return a < b && b < c;
    }

    static bool singularConstraint(int a, int b, int c, int d)
    {
        // in 4 cycle - a is smallest and the 4th is larger then the second (the two around the first - for singularity)
        // generally - this is the constraint in 3 cycle too
        return a < b && a < c && a < d && b < d;
    }

    public static List<List<int>> Find<T>(IEnumerable<int> nodes, Dictionary<int, Dictionary<int, T>> edges, int maxSize)
    {
        stopWatch.Start();
        Dictionary<int, PotentialLink> potentialLinks = new Dictionary<int, PotentialLink>();
        List<List<int>> res = new List<List<int>>();
        Dictionary<int, int> marks = nodes.ToDictionary(p => p, p => NON_VISITED);

        foreach (var nodeId in nodes)
        {
            stopWatch.Start();
            foreach (var edge1 in edges[nodeId])
            {
                foreach (var edge2 in edges[edge1.Key])
                {
                    if (edge2.Key != nodeId)
                    {
                        if (edges[edge2.Key].ContainsKey(nodeId))
                        {
                            // singularuty
                            if (singularConstraint(nodeId, edge1.Key, edge2.Key))
                                res.Add(new List<int>() { nodeId, edge1.Key, edge2.Key });
                        }
                        else
                        {
                            foreach (var edge3 in edges[edge2.Key])
                            {
                                if (edge3.Key != nodeId && edge3.Key != edge1.Key) // not returning to previous node
                                    if (edges[edge3.Key].ContainsKey(nodeId) && !edges[edge3.Key].ContainsKey(edge1.Key)) // connected to root node and not made of 2 triangles
                                        if (singularConstraint(nodeId, edge1.Key, edge2.Key, edge3.Key)) // singularuty
                                            res.Add(new List<int>() { nodeId, edge1.Key, edge2.Key, edge3.Key });
                            }
                        }
                    }
                }
            }
        }
        stopWatch.Stop();
        return res;
    }

    static T iCycle<T>(List<T> cycle, int i)
    {
        return cycle[mod(i, cycle.Count)];
    }
    static (int, int) formatNodesId(int id1, int id2)
    {
        return (id1 < id2) ? (id1, id2) : (id2, id1);
    }

    static int getOtherNode(ValueTuple<int, int> edge, int curr)
    {
        if (edge.Item1 == curr) return edge.Item2;
        else if (edge.Item2 == curr) return edge.Item1;
        else throw new InvalidOperationException("No edge found");

    }
    static double angleBetween(Tuple<float, float> p1, Tuple<float, float> p2)
    {
        return Math.Atan2(p2.Item2 - p1.Item2, p2.Item1 - p1.Item1);
    }

    public enum EDGE_TYPE
    {
        EDGE_EGDE = 0,
        SINGLE_NODE_CONNECTED_EGDE = 1,
        DITACHED_EGDE = 2,
        MARKED_AS_USED = 3,
    }

    public static EDGE_TYPE MinEdgeType(EDGE_TYPE a, EDGE_TYPE b)
    {
        return (int)a > (int)b ? b : a;
    }

    private static (Dictionary<(int, int), (int, int, EDGE_TYPE)>, Dictionary<int, EDGE_TYPE>) AnalyzeCEdges(List<List<int>> cycles, Dictionary<int, bool> isEdgeParticleDictionaty)
    {
        var edgesMap = new Dictionary<(int, int), (int, int, EDGE_TYPE)>();
        var cycleFlagMap = new Dictionary<int, EDGE_TYPE>();
        for (var c = 0; c < cycles.Count; c++)
        {
            //bool isAllParticlesEdgesParticles = true;
            var cycle = cycles[c];
            var minEdgeType = EDGE_TYPE.MARKED_AS_USED;
            for (var i = 0; i < cycle.Count; i++)
            {
                EDGE_TYPE edgeType;
                var edgeName = formatNodesId(iCycle(cycle, i), iCycle(cycle, i + 1));
                if (edgesMap.ContainsKey(edgeName))
                {
                    int firstNode, secontNode;
                    (firstNode, secontNode, edgeType) = edgesMap[edgeName];

                    if (secontNode != -1)
                    {
                        UnityEngine.Debug.LogAssertion(String.Format("prev c != -1 - {0} {1} {2}", cycle.Count, cycles[firstNode].Count, cycles[secontNode].Count));
                        throw new UnityEngine.UnityException();
                    }
                    edgesMap[edgeName] = (firstNode, c, edgeType);
                }
                else
                {
                    var edgeParticleCount = 0;
                    edgeParticleCount += isEdgeParticleDictionaty[iCycle(cycle, i)] ? 1 : 0;
                    edgeParticleCount += isEdgeParticleDictionaty[iCycle(cycle, i + 1)] ? 1 : 0;
                    edgeType = edgeParticleCount == 2 ? EDGE_TYPE.EDGE_EGDE : (edgeParticleCount == 1 ? EDGE_TYPE.SINGLE_NODE_CONNECTED_EGDE : EDGE_TYPE.DITACHED_EGDE);
                    edgesMap[edgeName] = (c, -1, edgeType);
                }
                minEdgeType = MinEdgeType(minEdgeType, edgeType);
            }
            cycleFlagMap[c] = minEdgeType;
        }
        return (edgesMap, cycleFlagMap);
    }

    public static List<ElementGroupPolygon> FindAdjacantCicles(List<List<int>> cycles, Func<int, bool> isEdgeParticlePredicate, Func<int, Tuple<float, float>> getItemPos)
    {

        var polygons = new List<ElementGroupPolygon>();

        var isEdgeParticleDictionaty = cycles.SelectMany(x => x).Distinct().ToDictionary(x => x, x => isEdgeParticlePredicate(x));

        // Removing cycles that are all in egde particles
        cycles = cycles.Where(nodes => nodes.Any(nodeId => !isEdgeParticleDictionaty[nodeId])).ToList();

        Dictionary<(int, int), (int, int, EDGE_TYPE)> edgesMap;
        Dictionary<int, EDGE_TYPE> cycleFlagMap;
        try
        {
            (edgesMap, cycleFlagMap) = AnalyzeCEdges(cycles, isEdgeParticleDictionaty);
        }
        catch (UnityEngine.UnityException)
        {
            return new List<ElementGroupPolygon>();
        }

        // Looping threw all the cycles found threw neighbors. marking each one already looped in map - `cycleFlagMap`
        // each while loop represent continuous cycles streak
        // First looking for EDGE_EGDE (touching existing cycle)
        var cyclesLeft = true;
        var requiredEdgeType = EDGE_TYPE.EDGE_EGDE;
        while (cyclesLeft)
        {
            KeyValuePair<int, EDGE_TYPE> unflagedCycle;
            try { unflagedCycle = cycleFlagMap.First(t => t.Value == requiredEdgeType); }
            catch (InvalidOperationException)
            {
                if (requiredEdgeType == EDGE_TYPE.EDGE_EGDE)
                    requiredEdgeType = EDGE_TYPE.DITACHED_EGDE;
                else
                    cyclesLeft = false;

                continue;
            }

            var groupNodes = new Dictionary<int, bool>();

            var endEgdes = new List<(int, int)>();

            Stack<int> cycleStack = new Stack<int>();
            List<List<int>> sourceCycles = new List<List<int>>();
            cycleStack.Push(unflagedCycle.Key);
            cycleFlagMap[unflagedCycle.Key] = EDGE_TYPE.MARKED_AS_USED;

            // At every loop - taking cycle from the stack and adding its neighbors to the stack
            while (cycleStack.Count > 0)
            {
                var cycleIndex = cycleStack.Pop();
                var cycle = cycles[cycleIndex];
                sourceCycles.Add(cycle);
                for (int e = 0; e < cycle.Count; e++)
                {
                    // For enlisting all nodes in list
                    groupNodes[iCycle(cycle, e)] = true;

                    var edgeName = formatNodesId(iCycle(cycle, e), iCycle(cycle, e + 1));
                    if (edgesMap[edgeName].Item2 == -1)
                    {
                        // end edge
                        endEgdes.Add(edgeName);
                    }
                    else
                    {
                        var cyclesInEdge = edgesMap[edgeName];
                        int theOtherCycle = -1;
                        if (cycleIndex == cyclesInEdge.Item1)
                            theOtherCycle = cyclesInEdge.Item2;
                        else if (cycleIndex == cyclesInEdge.Item2)
                            theOtherCycle = cyclesInEdge.Item1;
                        else
                            UnityEngine.Debug.LogAssertion("cycle not found :(");

                        // In case we done going threw the "touching the existing FEMs" edges, we wouldn't like to capture them as possible
                        // (Not sure if this condition will ever meet. just to be sure)
                        if (requiredEdgeType == EDGE_TYPE.DITACHED_EGDE && cycleFlagMap[theOtherCycle] == EDGE_TYPE.EDGE_EGDE || cycleFlagMap[theOtherCycle] == EDGE_TYPE.SINGLE_NODE_CONNECTED_EGDE)
                        {
                            // end edge
                            endEgdes.Add(edgeName);
                        }
                        else if (theOtherCycle >= 0 && cycleFlagMap[theOtherCycle] != EDGE_TYPE.MARKED_AS_USED)
                        {
                            cycleStack.Push(theOtherCycle);
                            cycleFlagMap[theOtherCycle] = EDGE_TYPE.MARKED_AS_USED;
                        }
                    }
                }
            }

            var test_nodes_positions = new Dictionary<int, Tuple<float, float>>();
            var nodeEdgeMap = new Dictionary<int, List<int>>();

            for (var i = 0; i < endEgdes.Count; i++)
            {
                var (aId, bId) = endEgdes[i];
                if (nodeEdgeMap.ContainsKey(aId))
                    nodeEdgeMap[aId].Add(i);
                else
                {
                    nodeEdgeMap[aId] = new List<int>() { i };
                    test_nodes_positions[aId] = getItemPos(aId);
                }
                if (nodeEdgeMap.ContainsKey(bId))
                    nodeEdgeMap[bId].Add(i);
                else
                {
                    nodeEdgeMap[bId] = new List<int>() { i };
                    test_nodes_positions[bId] = getItemPos(bId);
                }

            };

            var test_midY = (test_nodes_positions.Select(x => x.Value.Item2).Min() + test_nodes_positions.Select(x => x.Value.Item2).Max()) / 2;
            var text_minX = float.MaxValue;
            var text_minX_i = -1;
            var test_first = -1;
            var test_second = -1;

            for (var i = 0; i < endEgdes.Count; i++)
            {
                var (ind1, ind2) = endEgdes[i];
                var pos1 = test_nodes_positions[ind1];
                var pos2 = test_nodes_positions[ind2];
                if (pos1.Item2 > test_midY && pos2.Item2 <= test_midY && (pos1.Item1 + pos2.Item1) / 2 < text_minX)
                {
                    text_minX = (pos1.Item1 + pos2.Item1) / 2;
                    text_minX_i = i;
                    test_first = ind2;
                    test_second = ind1;
                }
                else if (pos2.Item2 > test_midY && pos1.Item2 <= test_midY && (pos1.Item1 + pos2.Item1) / 2 < text_minX)
                {
                    text_minX = (pos1.Item1 + pos2.Item1) / 2;
                    text_minX_i = i;
                    test_first = ind1;
                    test_second = ind2;
                }
            }

            var test_prev = test_first;
            var test_current = test_second;
            var pol = new List<int> { test_first, test_second };
            while (test_current != test_first)
            {
                var nextEgdes = nodeEdgeMap[test_current].Select(i => endEgdes[i]).Where(s => s != formatNodesId(test_prev, test_current)).ToList();
                var test_nextNode = -1;
                if (nextEgdes.Count > 1)
                {
                    var baseEdgeAngle = angleBetween(test_nodes_positions[test_current], test_nodes_positions[test_prev]);
                    var maxAngle = -double.MaxValue;
                    // var potential_next_node = -1;
                    foreach (var edge in nextEgdes)
                    {
                        var currentEdgeAngle = angleBetween(test_nodes_positions[test_current], test_nodes_positions[getOtherNode(edge, test_current)]);
                        var relativeCurrentAngle = mod(currentEdgeAngle - baseEdgeAngle, 2 * Math.PI);
                        if (relativeCurrentAngle > maxAngle)
                        {
                            maxAngle = relativeCurrentAngle;
                            test_nextNode = getOtherNode(edge, test_current);
                        }
                    }
                }
                else
                {
                    test_nextNode = getOtherNode(nextEgdes.First(), test_current);
                }
                test_prev = test_current;
                test_current = test_nextNode;
                pol.Add(test_current);
            }
            //new Dictionary<int,bool> 
            var test_polSet = new HashSet<int>(pol.Distinct());
            polygons.Add(new ElementGroupPolygon()
            {
                polygon = pol,
                holes = new List<List<int>>(),
                restElements = groupNodes.Where(nodeT => !test_polSet.Contains(nodeT.Key)).Select(x => x.Key).ToList(),
                isTouchingExistingFEM = requiredEdgeType == EDGE_TYPE.EDGE_EGDE,
                sourceCycles = sourceCycles,
            });
        }


        return polygons;
    }
}