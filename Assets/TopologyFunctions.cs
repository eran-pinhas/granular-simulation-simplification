﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Mesh
{
    public List<Tuple<float, float>> positions = new List<Tuple<float, float>>();
    public List<Tuple<int, int, int>> triangles = new List<Tuple<int, int, int>>();
    public List<Tuple<int, int>> links = new List<Tuple<int, int>>();
};

public class TopologyFunctions
{
    public static Matrix4x4 LinearRegression2d(IEnumerable<Tuple<float, float>> x, IEnumerable<Tuple<float, float>> y)
    {
        // https://newonlinecourses.science.psu.edu/stat501/node/382/ (except changing the vector order from [1 x1 x2...] to [x1 x2 ... 1])
        var n = x.Count();
        var x1Sum = x.Select(t => t.Item1).Sum();
        var x2Sum = x.Select(t => t.Item2).Sum();
        var x1x2Sum = x.Select(t => t.Item1 * t.Item2).Sum();
        var x1x1Sum = x.Select(t => t.Item1 * t.Item1).Sum();
        var x2x2Sum = x.Select(t => t.Item2 * t.Item2).Sum();
        var xxCol1 = new Vector4(x1x1Sum, x1x2Sum, x1Sum, 0);
        var xxCol2 = new Vector4(x1x2Sum, x2x2Sum, x2Sum, 0);
        var xxCol3 = new Vector4(x1Sum, x2Sum, n, 0);
        var xxCol4 = new Vector4(0, 0, 0, 1);
        var xx = new Matrix4x4(xxCol1, xxCol2, xxCol3, xxCol4);
        var xx_inv = xx.inverse;

        var y1Sum = y.Select(t => t.Item1).Sum();
        var y2Sum = y.Select(t => t.Item2).Sum();
        var x1y1Sum = y.Zip(x, (y_i, x_i) => y_i.Item1 * x_i.Item1).Sum();
        var x1y2Sum = y.Zip(x, (y_i, x_i) => y_i.Item2 * x_i.Item1).Sum();
        var x2y1Sum = y.Zip(x, (y_i, x_i) => y_i.Item1 * x_i.Item2).Sum();
        var x2y2Sum = y.Zip(x, (y_i, x_i) => y_i.Item2 * x_i.Item2).Sum();

        var xy1Vec = xx_inv * new Vector4(x1y1Sum, x2y1Sum, y1Sum, 0);
        var xy2Vec = xx_inv * new Vector4(x1y2Sum, x2y2Sum, y2Sum, 0);
        var res3Vec = new Vector4(0, 0, 1, 0);
        var res4Vec = new Vector4(0, 0, 0, 1);

        return new Matrix4x4(xy1Vec, xy2Vec, res3Vec, res4Vec).transpose;
    }


    public static Tuple<float, float> TranformPoint(Matrix4x4 tranform, Tuple<float, float> point)
    {
        return Tuple.Create(
            tranform[0, 0] * point.Item1 + tranform[0, 1] * point.Item2 + tranform[0, 2],
            tranform[1, 0] * point.Item1 + tranform[1, 1] * point.Item2 + tranform[1, 2]
            );
    }

    public static Tuple<Tuple<float, float>, Tuple<float, float>> ExtactExtent(List<Tuple<float, float>> points)
    {
        var xList = points.Select(p => p.Item1);
        var yList = points.Select(p => p.Item2);

        return Tuple.Create(
            Tuple.Create(xList.Min(), xList.Max()),
            Tuple.Create(yList.Min(), yList.Max())
            );

    }

    public static (Mesh, (float, float)) SimpleTriangleMesh(Tuple<Tuple<float, float>, Tuple<float, float>> extent, float meshSize, ValueTuple<float, float>? forceBLCorner = null)
    {
        var xStep = meshSize;
        var yStep = meshSize * (float)Math.Sqrt(3) / 2;

        var ((minX, maxX), (minY, maxY)) = extent;

        //if (forceBLCorner.HasValue)
        //{
        //    var (newMinX, newMinY) = forceBLCorner.Value;
        //    while (newMinX > minX) newMinX -= xStep;
        //    while (newMinY > minY) newMinY -= yStep;
        //    minY = newMinY;
        //    minX = newMinX;
        //}

        var startXI = (int)Math.Floor(minX / xStep);
        var startYI = (int)Math.Floor(minY / xStep);

        var mesh = new Mesh();
        List<int> lastRow = null;
        for (var yi = startYI; (yi - 1) * yStep < maxY; yi++)
        {
            var y = yi * yStep;
            bool evenRow = yi % 2 == 0;
            List<int> currentRow = new List<int>();
            var deltaX = evenRow ? 0 : xStep / 2;
            for (var xi = startXI; (xi - 1) * xStep + deltaX < maxX; xi++)
            {
                var x = xi * xStep + deltaX;
                mesh.positions.Add(Tuple.Create(x, y));
                var index = mesh.positions.Count - 1;
                currentRow.Add(index);
                var innerRowIndex = currentRow.Count - 1;

                // in the same line - connect to previous
                if (innerRowIndex > 0) mesh.links.Add(new Tuple<int, int>(index - 1, index));
                // connections to previous line
                if (lastRow != null)
                {
                    if (evenRow)
                    {
                        if (innerRowIndex + 1 < lastRow.Count) mesh.links.Add(new Tuple<int, int>(index, lastRow[innerRowIndex]));
                        if (innerRowIndex > 0) mesh.links.Add(new Tuple<int, int>(index, lastRow[innerRowIndex - 1]));
                    }
                    else
                    {
                        mesh.links.Add(new Tuple<int, int>(index, lastRow[innerRowIndex]));
                        if (innerRowIndex + 1 < lastRow.Count) mesh.links.Add(new Tuple<int, int>(index, lastRow[innerRowIndex + 1]));
                    }
                }
            }
            lastRow = currentRow;
        }
        return (mesh, (minX, minY));
    }

    struct Point
    {
        public float x;
        public float y;
    };

    // Given three colinear points p, q, r, the function checks if 
    // point q lies on line segment 'pr' 
    private static bool onSegment(Point p, Point q, Point r)
    {
        if (q.x <= Math.Max(p.x, r.x) && q.x >= Math.Min(p.x, r.x) &&
                q.y <= Math.Max(p.y, r.y) && q.y >= Math.Min(p.y, r.y))
            return true;
        return false;
    }

    // To find orientation of ordered triplet (p, q, r). 
    // The function returns following values 
    // 0 --> p, q and r are colinear 
    // 1 --> Clockwise 
    // 2 --> Counterclockwise 
    private static int orientation(Point p, Point q, Point r)
    {
        float val = (q.y - p.y) * (r.x - q.x) -
                  (q.x - p.x) * (r.y - q.y);

        if (val == 0) return 0;  // colinear 
        return (val > 0) ? 1 : 2; // clock or counterclock wise 
    }

    private static Point ToPoint(Tuple<float, float> t)
    {
        return new Point() { x = t.Item1, y = t.Item2 };
    }
    public static bool DoIntersect2(Tuple<float, float> p1, Tuple<float, float> q1, Tuple<float, float> p2, Tuple<float, float> q2)
    {
        return doIntersect(ToPoint(p1), ToPoint(q1), ToPoint(p2), ToPoint(q2));
    }
    public static Tuple<float, float> Substract(Tuple<float, float> a, Tuple<float, float> b)
    {
        return new Tuple<float, float>(a.Item1 - b.Item1, a.Item2 - b.Item2);
    }
    public static Tuple<float, float> Add(Tuple<float, float> a, Tuple<float, float> b)
    {
        return new Tuple<float, float>(a.Item1 + b.Item1, a.Item2 + b.Item2);
    }
    public static Tuple<float, float> Normalize(Tuple<float, float> a)
    {
        var d = Distance(new Tuple<float, float>(0, 0), a);
        return new Tuple<float, float>(a.Item1 /d , a.Item2 /d);
    }
    public static Tuple<float, float> Scale(Tuple<float, float> a, float k)
    {
        return new Tuple<float, float>(a.Item1 * k, a.Item2 * k);
    }

    public static T MaxBy<T>(IEnumerable<T> a, Func<T, float> acti) where T : class
    {
        Debug.Assert(a.Count() != 0, "a.Count() is 0");
        var res = a.Aggregate((null as T, float.MinValue), (prev, next) =>
        {
            var s = acti(next);
            return s > prev.Item2 ? (next, s) : prev;
        });
        return res.Item1;
    }


    public static IEnumerable<Tuple<float, float>> GetPathEvery(IEnumerable<Tuple<float, float>> path, float interval)
    {

        var res = path.Aggregate((0f, null as Tuple<float, float>, new List<Tuple<float, float>>()), (prev, currentPoint) =>
        {
            var leftFromLastSection = prev.Item1;
            var lastPoint = prev.Item2;
            var results = prev.Item3;
            if (lastPoint == null)
                return (0, currentPoint, results);
            else
            {
                var currentP = -leftFromLastSection;
                var d = Distance(lastPoint, currentPoint);
                var direction = Normalize( Substract(currentPoint, lastPoint));
                while(currentP+ interval <= d)
                {
                    currentP += interval;
                    results.Add(Add(lastPoint, Scale(direction, currentP)));
                }
                return (d - currentP, currentPoint, results);
            }
        });
        return res.Item3;
    }




    // The function that returns true if line segment 'p1q1' 
    // and 'p2q2' intersect. 
    private static bool doIntersect(Point p1, Point q1, Point p2, Point q2)
    {
        // Find the four orientations needed for general and 
        // special cases 
        int o1 = orientation(p1, q1, p2);
        int o2 = orientation(p1, q1, q2);
        int o3 = orientation(p2, q2, p1);
        int o4 = orientation(p2, q2, q1);

        // General case 
        if (o1 != o2 && o3 != o4)
            return true;

        // Special Cases 
        // p1, q1 and p2 are colinear and p2 lies on segment p1q1 
        if (o1 == 0 && onSegment(p1, p2, q1)) return true;

        // p1, q1 and p2 are colinear and q2 lies on segment p1q1 
        if (o2 == 0 && onSegment(p1, q2, q1)) return true;

        // p2, q2 and p1 are colinear and p1 lies on segment p2q2 
        if (o3 == 0 && onSegment(p2, p1, q2)) return true;

        // p2, q2 and q1 are colinear and q1 lies on segment p2q2 
        if (o4 == 0 && onSegment(p2, q1, q2)) return true;

        return false; // Doesn't fall in any of the above cases 
    }

    public static double PolsbyPopper(List<Tuple<float, float>> poly)
    {
        var points = new List<Tuple<float, float>>(poly);
        points.Add(points[0]);
        var area = Math.Abs(points.Take(points.Count - 1)
        .Select((p, i) => (points[i + 1].Item1 - p.Item1) * (points[i + 1].Item2 + p.Item2))
        .Sum() / 2);

        var perimiter = poly.Select((p, i) => Distance(points[i + 1], p)).Sum();

        return Math.PI * 4 * area / (perimiter * perimiter);
    }

    // Returns true if the point p lies inside the polygon[] with n vertices 
    public static bool PointInPolygon(List<Tuple<float, float>> inPolygon, Tuple<float, float> pIn, float bufferInside)
    {

        var bufferInsideSquared = bufferInside * bufferInside;
        int n = inPolygon.Count;
        var polygon = inPolygon.Select(ToPoint).ToList();
        var p = ToPoint(pIn);
        // There must be at least 3 vertices in polygon[] 
        if (n < 3) return false;

        if (inPolygon.Any(pNode => DistanceSquared(pNode, pIn) < bufferInsideSquared))
            return false;

        // Create a point for line segment from p to infinite 
        Point extreme = new Point() { x = 100000000, y = p.y };

        // Count intersections of the above line with sides of polygon 
        int count = 0, i = 0;
        do
        {
            int next = (i + 1) % n;

            // Check if the line segment from 'p' to 'extreme' intersects 
            // with the line segment from 'polygon[i]' to 'polygon[next]' 
            if (doIntersect(polygon[i], polygon[next], p, extreme))
            {
                // If the point 'p' is colinear with line segment 'i-next', 
                // then check if it lies on segment. If it lies, return true, 
                // otherwise false 
                if (orientation(polygon[i], p, polygon[next]) == 0)
                    return onSegment(polygon[i], p, polygon[next]);

                count++;
            }
            i = next;
        } while (i != 0);

        // Return true if count is odd, false otherwise 
        return count % 2 == 1;  // Same as (count%2 == 1) 
    }

    public static Tuple<float, float> CenterOf(Tuple<float, float> a, Tuple<float, float> b)
    {
        return new Tuple<float, float>((a.Item1 + b.Item1) / 2, (a.Item2 + b.Item2) / 2);
    }
    private static float DistanceSquared(Tuple<float, float> a, Tuple<float, float> b)
    {
        return ((a.Item1 - b.Item1) * (a.Item1 - b.Item1) + (a.Item2 - b.Item2) * (a.Item2 - b.Item2));
    }
    public static float Distance(Tuple<float, float> a, Tuple<float, float> b)
    {
        return (float)Math.Sqrt(DistanceSquared(a, b));
    }
    public static float DistanceLineToPoint(Tuple<float, float> lineA, Tuple<float, float> lineB, Tuple<float, float> point)
    {
        // https://en.wikipedia.org/wiki/Distance_from_a_point_to_a_line
        var (x0, y0) = point;
        var (x1, y1) = lineA;
        var (x2, y2) = lineB;
        return Math.Abs((x2 - x1) * (y1 - x0) - (x1 - x0) * (y2 - x1)) / Distance(lineA, lineB);
    }

    public static List<ValueTuple<int, int>> ConnectOuterPolygonToMesh(List<Tuple<float, float>> polygon, Mesh mesh)
    {
        int MOD = 2;
        var polygonLength = polygon.Count - 1;
        var links = new List<ValueTuple<int, int>>();

        var lengthSquaredDict = new Dictionary<(int, int), float>();

        var twoNearestNeighborsDict = new Dictionary<int, (int, int)>();
        var minSumSquareDict = new Dictionary<int, float>();

        for (var i = 0; i < polygon.Count; i++)
        {
            var pI = polygon[i];
            var nearestNeighbor = -1;
            var nearestNeighbor2 = -1;
            for (var j = 0; j < mesh.positions.Count; j++)
            {
                var mJ = mesh.positions[j];
                float l = DistanceSquared(pI, mJ);
                if (nearestNeighbor < 0 || l < lengthSquaredDict[(i, nearestNeighbor)])
                {
                    nearestNeighbor2 = nearestNeighbor;
                    nearestNeighbor = j;
                }
                else if (nearestNeighbor2 < 0 || l < lengthSquaredDict[(i, nearestNeighbor2)])
                {
                    nearestNeighbor2 = j;
                }
                lengthSquaredDict[(i, j)] = l;
            }
            twoNearestNeighborsDict[i] = (nearestNeighbor, nearestNeighbor2);
            minSumSquareDict[i] = lengthSquaredDict[(i, nearestNeighbor)] + lengthSquaredDict[(i, nearestNeighbor2)];

        }

        if (MOD == 2)
        {
            return twoNearestNeighborsDict
                .Where(kv => kv.Key < polygon.Count - 1)
                .SelectMany(kv => new List<(int, int)>() { (kv.Key, kv.Value.Item1), (kv.Key, kv.Value.Item2) })
                .Distinct()
                .ToList();
        }

        var prevNodeIndex = polygonLength - 1;
        var switchNodes = new List<int>();
        for (var i = 0; i < polygonLength; i++)
        {
            if (minSumSquareDict[i] < minSumSquareDict[i + 1] && minSumSquareDict[i] < minSumSquareDict[prevNodeIndex])
            {
                switchNodes.Add(i);
            }
            prevNodeIndex = i;
        }

        for (var i = 0; i < switchNodes.Count; i++)
        {
            var fromPolygonIndex = switchNodes[i];
            var toPolygonIndex = switchNodes[(i + 1) % switchNodes.Count];

            var toMin = twoNearestNeighborsDict[toPolygonIndex];
            var fromMin = twoNearestNeighborsDict[toPolygonIndex];

            var commonMeshNode = -1;
            if (toMin.Item1 == fromMin.Item1) commonMeshNode = toMin.Item1;
            if (toMin.Item1 == fromMin.Item2) commonMeshNode = toMin.Item1;
            if (toMin.Item2 == fromMin.Item1) commonMeshNode = toMin.Item2;
            if (toMin.Item2 == fromMin.Item2) commonMeshNode = toMin.Item2;

            // the two switch nodes have a common node in the mesh
            // meaning all the node between should connect to it
            if (commonMeshNode != -1)
            {
                // var ; // without +1
                for (var j = fromPolygonIndex + 1; j != toPolygonIndex; j = (j + 1) % polygonLength) // cyclic for
                {
                    links.Add((j, commonMeshNode));
                }
                //links.Add((j, commonMeshNode));
            }
            // the two switch nodes doesn't have a common node in the mesh
            else
            {
                var lastConnection = -1;
                var lastConnectionFrom = -1;
                for (var j = fromPolygonIndex + 1; j != toPolygonIndex; j = (j + 1) % polygonLength) // cyclic for
                {
                    var nn = twoNearestNeighborsDict[i].Item1;
                    // same nearest neighbor - only connection is the polygon & mesh nearest neighbor
                    if (lastConnection == -1 || nn == lastConnection) { }
                    // othen nearest neighbor - for triangulation we need a cross link
                    else
                    {
                        if (DistanceSquared(polygon[lastConnectionFrom], mesh.positions[nn]) < DistanceSquared(polygon[j], mesh.positions[lastConnection]))
                            links.Add((lastConnectionFrom, nn));
                        else
                            links.Add((j, lastConnection));
                    }

                    links.Add((j, nn));
                    lastConnection = nn;
                    lastConnectionFrom = j;
                    links.Add((j, commonMeshNode));
                }
            }

            // connecting the swicthed node to both mesh nodes it is between
            links.Add((switchNodes[i], twoNearestNeighborsDict[switchNodes[i]].Item1));
            links.Add((switchNodes[i], twoNearestNeighborsDict[switchNodes[i]].Item2));
        }
        return links.Distinct().ToList();
    }
}
