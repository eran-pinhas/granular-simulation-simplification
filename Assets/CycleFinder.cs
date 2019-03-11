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
    }

    const int NON_VISITED = 0;
    const int VISITED = 1;
    const int DONE = 2;

    static int mod(int x, int m)
    {
        int r = x % m;
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
    static string stringFromNodesId(int id1, int id2)
    {
        return (id1 < id2) ? id1.ToString() + "_" + id2.ToString() : id2.ToString() + "_" + id1.ToString();
    }
    static List<int> nodesIdFromString(string str)
    {
        return str.Split('_').Select(int.Parse).ToList();
    }

    public static List<ElementGroupPolygon> FindAdjacantCicles(List<List<int>> cycles, Func<int, bool> isEdgeParticlePredicate)
    {
        const int EDGE_EGDE = 0;
        const int SINGLE_NODE_CONNECTED_EGDE = 1;
        const int DITACHED_EGDE = 2;
        const int MARKED_AS_USED = 3;

        var polygons = new List<ElementGroupPolygon>();
        Dictionary<string, ValueTuple<int, int, int>> edgesMap = new Dictionary<string, ValueTuple<int, int, int>>();


        var isEdgeParticleDictionaty = cycles.SelectMany(x => x).Distinct().ToDictionary(x => x, x => isEdgeParticlePredicate(x));

        // Removing cycles that are all in egde particles
        cycles = cycles.Where(nodes => nodes.Any(nodeId => !isEdgeParticleDictionaty[nodeId])).ToList();

        Dictionary<int, int> cycleFlagMap = new Dictionary<int, int>();
        for (var c = 0; c < cycles.Count; c++)
        {
            //bool isAllParticlesEdgesParticles = true;
            var cycle = cycles[c];
            var minEdgeType = int.MaxValue;
            for (var i = 0; i < cycle.Count; i++)
            {
                int edgeType;
                string edgeName = stringFromNodesId(iCycle(cycle, i), iCycle(cycle, i + 1));
                if (edgesMap.ContainsKey(edgeName))
                {
                    int firstNode, secontNode;
                    (firstNode, secontNode, edgeType) = edgesMap[edgeName];

                    UnityEngine.Debug.Assert(secontNode == -1, "prev c != -1");
                    edgesMap[edgeName] = (firstNode, c, edgeType);
                }
                else
                {
                    var edgeParticleCount = 0;
                    edgeParticleCount += isEdgeParticlePredicate(iCycle(cycle, i)) ? 1 : 0;
                    edgeParticleCount += isEdgeParticlePredicate(iCycle(cycle, i + 1)) ? 1 : 0;
                    edgeType = edgeParticleCount == 2 ? EDGE_EGDE : (edgeParticleCount == 1 ? SINGLE_NODE_CONNECTED_EGDE : DITACHED_EGDE);
                    edgesMap[edgeName] = (c, -1, edgeType);
                }

                //isAllParticlesEdgesParticles = isAllParticlesEdgesParticles && (edgeType == EDGE_EGDE);

                minEdgeType = Math.Min(minEdgeType, edgeType);
            }

            //if (!isAllParticlesEdgesParticles)
            cycleFlagMap[c] = minEdgeType;
        }


        // Looping threw all the cycles found threw neighbors. marking each one already looped in map - `cycleFlagMap`
        // each while loop represent continuous cycles streak
        var cyclesLeft = true;
        var requiredEdgeType = EDGE_EGDE;
        while (cyclesLeft)
        {
            KeyValuePair<int, int> unflagedCycle;
            try { unflagedCycle = cycleFlagMap.First(t => t.Value == requiredEdgeType); }
            catch (InvalidOperationException)
            {
                if (requiredEdgeType == EDGE_EGDE)
                {
                    requiredEdgeType = DITACHED_EGDE;
                    //    cycleFlagMap.Where(t => t.Key == SINGLE_NODE_CONNECTED_EGDE).ToList().ForEach(t => cycleFlagMap[t.Key] = MARKED_AS_USED);
                }
                else
                {
                    cyclesLeft = false;
                }
                continue;
            }

            var allNodes = new Dictionary<int, bool>();

            List<string> endEgdes = new List<string>();

            Stack<int> cycleStack = new Stack<int>();
            List<List<int>> sourceCycles = new List<List<int>>();
            cycleStack.Push(unflagedCycle.Key);
            cycleFlagMap[unflagedCycle.Key] = MARKED_AS_USED;

            // At every loop - taking cycle from the stack and adding its neighbors to the stack
            while (cycleStack.Count > 0)
            {
                var cycleIndex = cycleStack.Pop();
                var cycle = cycles[cycleIndex];
                sourceCycles.Add(cycle);
                for (int e = 0; e < cycle.Count; e++)
                {
                    // For enlisting all nodes in list
                    allNodes[iCycle(cycle, e)] = true;

                    var edgeName = stringFromNodesId(iCycle(cycle, e), iCycle(cycle, e + 1));
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

                        // In case we done going threw the "touching the existing FEM" edges, we wouldn't like to capture them as possible
                        // (Not sure if this condition will ever meet. just to be sure)
                        if (requiredEdgeType == DITACHED_EGDE && cycleFlagMap[theOtherCycle] == EDGE_EGDE || cycleFlagMap[theOtherCycle] == SINGLE_NODE_CONNECTED_EGDE)
                        {
                            // end edge
                            endEgdes.Add(edgeName);
                        }
                        else if (theOtherCycle >= 0 && cycleFlagMap[theOtherCycle] != MARKED_AS_USED)
                        {
                            cycleStack.Push(theOtherCycle);
                            cycleFlagMap[theOtherCycle] = MARKED_AS_USED;
                        }
                    }
                }
            }

            Dictionary<int, List<int>> nodeEdgeMap = new Dictionary<int, List<int>>();
            for (var i = 0; i < endEgdes.Count; i++)
            {
                var edge = endEgdes[i];
                nodesIdFromString(edge).ForEach(nodeId =>
                {
                    if (nodeEdgeMap.ContainsKey(nodeId))
                    {
                        nodeEdgeMap[nodeId].Add(i);
                    }
                    else
                    {
                        nodeEdgeMap[nodeId] = new List<int>() { i };
                    }

                });
            };

            var strings = new List<List<int>>();
            var endEgdesFlags = endEgdes.ToDictionary(t => t, t => false);

            bool selfTouchingPolygon = false;
            while (!selfTouchingPolygon)
            {
                KeyValuePair<string, bool> startEgde;
                try
                {
                    startEgde = endEgdesFlags.First(t => !t.Value);
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                var startEgdeList = nodesIdFromString(startEgde.Key);
                endEgdesFlags[startEgde.Key] = true;
                var initNode = startEgdeList[0];
                var lastNode = startEgdeList[0];
                var currentNode = startEgdeList[1];

                var currentNodeString = new List<int>(startEgdeList);

                while (currentNode != initNode && !selfTouchingPolygon)
                {
                    var nextEgdes = nodeEdgeMap[currentNode].Select(i => endEgdes[i]).Where(s => s != stringFromNodesId(lastNode, currentNode));
                    if (nextEgdes.Count() != 1)
                    {
                        // is not usable polygon, we'll need to wait
                        selfTouchingPolygon = true;
                    }
                    endEgdesFlags[nextEgdes.First()] = true;
                    var nextNode = nodesIdFromString(nextEgdes.First()).First(x => x != currentNode);

                    lastNode = currentNode;
                    currentNode = nextNode;
                    currentNodeString.Add(currentNode);
                }

                strings.Add(currentNodeString);
            }

            if (!selfTouchingPolygon)
            {
                var outRing = TopologyFunctions.LongestString(strings);
                polygons.Add(new ElementGroupPolygon()
                {
                    polygon = outRing,
                    holes = strings.Where(p => p != outRing).ToList(),
                    restElements = allNodes.Where(nodeT => !nodeEdgeMap.ContainsKey(nodeT.Key)).Select(x => x.Key).ToList(),
                    isTouchingExistingFEM = requiredEdgeType == EDGE_EGDE,
                    sourceCycles = sourceCycles,
                });
            }
        }


        return polygons;
    }
}