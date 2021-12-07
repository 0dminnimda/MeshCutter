// Adapted from https://www.flipcode.com/archives/Efficient_Polygon_Triangulation.shtml

using System.Collections.Generic;
using UnityEngine;

class Triangulator
{
    // compute area of a contour/polygon
    public static float Area(List<Vector2> contour)
    {
        int n = contour.Count;

        float A = 0.0f;

        for (int p = n - 1, q = 0; q < n; p = q++)
        {
            A += contour[p].x * contour[q].y - contour[q].x * contour[p].y;
        }
        return A * 0.5f;
    }

    // decide if point Px/Py is inside triangle defined by
    // (Ax,Ay) (Bx,By) (Cx,Cy)
    public static bool InsideTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
    {
        float ax, ay, bx, by, cx, cy, apx, apy, bpx, bpy, cpx, cpy;
        float cCROSSap, bCROSScp, aCROSSbp;

        ax = C.x - B.x; ay = C.y - B.y;
        bx = A.x - C.x; by = A.y - C.y;
        cx = B.x - A.x; cy = B.y - A.y;
        apx = P.x - A.x; apy = P.y - A.y;
        bpx = P.x - B.x; bpy = P.y - B.y;
        cpx = P.x - C.x; cpy = P.y - C.y;

        aCROSSbp = ax * bpy - ay * bpx;
        cCROSSap = cx * apy - cy * apx;
        bCROSScp = bx * cpy - by * cpx;

        return ((aCROSSbp >= 0.0f) && (bCROSScp >= 0.0f) && (cCROSSap >= 0.0f));
    }

    public static bool Snip(List<Vector2> contour, int u, int v, int w, int n, int[] V)
    {
        int p;
        Vector2 P;
        Vector2 A = contour[V[u]];
        Vector2 B = contour[V[v]];
        Vector2 C = contour[V[w]];

        float smallEpsilon = 0.1f;
        if (Mathf.Epsilon > (((B.x - A.x) * (C.y - A.y)) - ((B.y - A.y) * (C.x - A.x))) &&
            Vector2.SqrMagnitude(A - B) > smallEpsilon &&
            Vector2.SqrMagnitude(A - C) > smallEpsilon &&
            Vector2.SqrMagnitude(B - C) > smallEpsilon)
            return false;
        // if (Mathf.Epsilon > (((Bx - Ax) * (Cy - Ay)) - ((By - Ay) * (Cx - Ax)))) return false;

        for (p = 0; p < n; p++)
        {
            if ((p == u) || (p == v) || (p == w)) continue;
            P = contour[V[p]];
            if (InsideTriangle(A, B, C, P)) return false;
        }

        return true;
    }

    // triangulate a contour/polygon, places results in STL vector
    // as series of triangles.
    public static bool Triangulate(List<Vector2> contour, List<int> result)
    {
        /* allocate and initialize list of Vertices in polygon */

        int n = contour.Count;
        if (n < 3)
            return false;

        int[] V = new int[n];

        /* we want a counter-clockwise polygon in V */

        if (0.0f < Area(contour))
            for (int v = 0; v < n; v++) V[v] = v;
        else
            for (int v = 0; v < n; v++) V[v] = (n - 1) - v;

        int nv = n;

        /*  remove nv-2 Vertices, creating 1 triangle every time */
        int count = 2 * nv;   /* error detection */

        for (int m = 0, v = nv - 1; nv > 2;)
        {
            /* if we loop, it is probably a non-simple polygon */
            if (0 >= (count--))
            {
                //** Triangulate: ERROR - probable bad polygon!
                return false;
            }

            /* three consecutive vertices in current polygon, <u,v,w> */
            int u = v; if (nv <= u) u = 0;     /* previous */
            v = u + 1; if (nv <= v) v = 0;     /* new v    */
            int w = v + 1; if (nv <= w) w = 0;     /* next     */

            if (Snip(contour, u, v, w, nv, V))
            {
                int a, b, c, s, t;

                /* true names of the vertices */
                a = V[u]; b = V[v]; c = V[w];

                /* output Triangle */
                // contour[a]
                result.Add(a);
                result.Add(b);
                result.Add(c);

                m++;

                /* remove v from remaining polygon */
                for (s = v, t = v + 1; t < nv; s++, t++) V[s] = V[t]; nv--;

                /* resest error detection counter */
                count = 2 * nv;
            }
        }

        return true;
    }

}
