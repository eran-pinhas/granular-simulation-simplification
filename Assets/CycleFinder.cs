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

    public static List<ElementGroupPolygon> FindAdjacantCicles(List<List<int>> cycles)
    {
        var polygons = new List<ElementGroupPolygon>();
        Dictionary<string, Tuple<int, int>> edgesMap = new Dictionary<string, Tuple<int, int>>();

        Dictionary<int, bool> cycleFlagMap = new Dictionary<int, bool>();
        for (var c = 0; c < cycles.Count; c++)
        {
            cycleFlagMap[c] = false;
            var cycle = cycles[c];

            for (var i = 0; i < cycle.Count; i++)
            {
                string edgeName = stringFromNodesId(iCycle(cycle, i), iCycle(cycle, i + 1));
                if (edgesMap.ContainsKey(edgeName))
                {
                    UnityEngine.Debug.Assert(edgesMap[edgeName].Item2 == -1, "prev c != -1");
                    edgesMap[edgeName] = Tuple.Create(edgesMap[edgeName].Item1, c);
                }
                else
                {
                    edgesMap[edgeName] = Tuple.Create(c, -1);
                }
            }
        }

        var cyclesLeft = true;

        // Looping threw all the cycles found threw neighbors. marking each one already looped in map - `cycleFlagMap`
        // each while loop represent continuous cycles streak
        while (cyclesLeft)
        {
            KeyValuePair<int, bool> unflagedCycle;
            try { unflagedCycle = cycleFlagMap.First(t => !t.Value); }
            catch (InvalidOperationException)
            {
                cyclesLeft = false;
                continue;
            }

            var allNodes = new Dictionary<int, bool>();

            List<string> endEgdes = new List<string>();

            Stack<int> cycleStack = new Stack<int>();
            cycleStack.Push(unflagedCycle.Key);
            cycleFlagMap[unflagedCycle.Key] = true;

            // At every loop - taking cycle from the stack and adding its neighbors to the stack
            while (cycleStack.Count > 0)
            {
                var cycleIndex = cycleStack.Pop();
                var cycle = cycles[cycleIndex];
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

                        if (theOtherCycle >= 0 && cycleFlagMap[theOtherCycle] == false)
                        {
                            cycleStack.Push(theOtherCycle);
                            cycleFlagMap[theOtherCycle] = true;
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

            if (!selfTouchingPolygon) {
                var outRing = TopologyFunctions.LongestString(strings);
                polygons.Add(new ElementGroupPolygon()
                {
                    polygon = outRing,
                    holes = strings.Where(p => p != outRing).ToList(),
                    restElements = allNodes.Where(nodeT => !nodeEdgeMap.ContainsKey(nodeT.Key)).Select(x=>x.Key).ToList(),
                });
            }
        }


        return polygons;
    }
}