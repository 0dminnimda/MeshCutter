using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static System.Math;
using static System.Diagnostics.Debug;

public class SliceScript : MonoBehaviour
{
    [SerializeField]
    Transform slicer;

    [SerializeField]
    GameObject[] slicees;

    (Mesh, Transform)[] data;

    Dictionary<(int, int), List<int>> lineToTrig;
    Dictionary<(int, int), Vector3> lineToIntersection;

    float[] vertexValues;

    static Dictionary<int, int> sideToInd = new Dictionary<int, int>
    {
        {3, 1},  // 1, 2 -> 1
        {4, 0},  // 1, 3 -> 0
        {5, 2},  // 2, 3 -> 2
    };

    void Start()
    {
        data = new (Mesh, Transform)[slicees.Length];
        for (int i = 0; i < slicees.Length; i++)
        {
            var obj = slicees[i];
            data[i] = (obj.GetComponent<MeshFilter>().mesh, obj.GetComponent<Transform>());
        }
    }

    void Update()
    {
        if (Input.GetKeyDown("space"))
        {
            Slice();
        }
    }

    void Slice()
    {
        for (int i = 0; i < data.Length; i++)
        {
            var (mesh, t) = data[i];
            var (mesh1, mesh2) = SliceMesh(mesh, t);
            var obj = slicees[i];

            t = obj.GetComponentInParent<Transform>();

            if (t == null || t.gameObject == obj)
            {
                var obj1 = Instantiate(obj, obj.transform.position, Quaternion.identity);
                obj1.GetComponent<MeshFilter>().mesh = mesh1;

                var obj2 = Instantiate(obj, obj.transform.position, Quaternion.identity);
                obj2.GetComponent<MeshFilter>().mesh = mesh2;
            }
            else
            {
                var obj1 = Instantiate(obj, obj.transform.position, Quaternion.identity, t);
                obj1.GetComponent<MeshFilter>().mesh = mesh1;

                var obj2 = Instantiate(obj, obj.transform.position, Quaternion.identity, t);
                obj2.GetComponent<MeshFilter>().mesh = mesh2;
            }

            Destroy(obj);
        }
    }

    (int, int) Sorted(int a, int b)
    {
        return (Min(a, b), Max(a, b));
    }

    void AddToLineToTrig((int, int) key, int value)
    {
        List<int> collection;
        if (!lineToTrig.TryGetValue(key, out collection))
        {
            collection = new List<int>();
            lineToTrig.Add(key, collection);
        }
        collection.Add(value);
    }

    void FillLineToTrig(Mesh mesh)
    {
        lineToTrig = new Dictionary<(int, int), List<int>>();

        for (int ind = 0; ind < mesh.triangles.Length; ind += 3)
        {
            var i1 = mesh.triangles[ind + 0];
            var i2 = mesh.triangles[ind + 1];
            var i3 = mesh.triangles[ind + 2];
            AddToLineToTrig(Sorted(i1, i2), ind);
            AddToLineToTrig(Sorted(i1, i3), ind);
            AddToLineToTrig(Sorted(i2, i3), ind);
        }
    }

    void CaclulateVetrexValues(Vector3 normal, Vector3 planePoint, Vector3[] vertices)
    {
        vertexValues = new float[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            vertexValues[i] = Vector3.Dot(vertices[i] - planePoint, normal);
        }
    }

    void AddVertex(Mesh mesh, List<int> locTriangles, List<Vector3> locVertices, int i)
    {
        var vertex = mesh.vertices[mesh.triangles[i]];
        var ind = locVertices.IndexOf(vertex);
        if (ind != -1)
            locTriangles.Add(ind);
        else
        {
            locVertices.Add(vertex);
            locTriangles.Add(locVertices.Count - 1);
        }
    }

    (Mesh, Mesh) CreateMeshes(Mesh mesh)
    {
        var triangles1 = new List<int>();
        var vertices1 = new List<Vector3>();

        var triangles2 = new List<int>();
        var vertices2 = new List<Vector3>();

        List<int> trianglesToAdd;
        List<Vector3> verticesToAdd;

        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            var i1 = mesh.triangles[i + 0];
            var i2 = mesh.triangles[i + 1];
            var i3 = mesh.triangles[i + 2];

            if (vertexValues[i1] > 0 && vertexValues[i2] > 0 && vertexValues[i3] > 0)
            {
                verticesToAdd = vertices1;
                trianglesToAdd = triangles1;
            }
            else if (vertexValues[i1] < 0 && vertexValues[i2] < 0 && vertexValues[i3] < 0)
            {
                verticesToAdd = vertices2;
                trianglesToAdd = triangles2;
            }
            else  // triangle is intersected
            {
                SliceTriangle(mesh, i, i1, i2, i3, triangles1, vertices1, triangles2, vertices2);
                continue;
            }

            verticesToAdd.Add(mesh.vertices[i1]);
            verticesToAdd.Add(mesh.vertices[i2]);
            verticesToAdd.Add(mesh.vertices[i3]);

            trianglesToAdd.Add(verticesToAdd.Count - 3);
            trianglesToAdd.Add(verticesToAdd.Count - 2);
            trianglesToAdd.Add(verticesToAdd.Count - 1);
        }

        var mesh1 = new Mesh();
        mesh1.vertices = vertices1.ToArray();
        mesh1.triangles = triangles1.ToArray();

        var mesh2 = new Mesh();
        mesh2.vertices = vertices2.ToArray();
        mesh2.triangles = triangles2.ToArray();

        return (mesh1, mesh2);
    }

    private void SliceTriangle(
        Mesh mesh, int i, int i1, int i2, int i3,
        List<int> triangles1, List<Vector3> vertices1,
        List<int> triangles2, List<Vector3> vertices2)
    {
        var points = new List<Vector3>(2);
        var indices = new List<int>(2);

        var (succ, vec, scalar) = LinePlaneIntersectionByIndex(mesh.vertices, i1, i2);
        if (succ) { points.Add(vec); indices.Add(1); }

        (succ, vec, scalar) = LinePlaneIntersectionByIndex(mesh.vertices, i1, i3);
        if (succ) { points.Add(vec); indices.Add(3); }

        (succ, vec, scalar) = LinePlaneIntersectionByIndex(mesh.vertices, i2, i3);
        if (succ) { points.Add(vec); indices.Add(2); }

        Assert(points.Count == 2);
        var (point1, point2) = (points[0], points[1]);

        Assert(indices.Count == 2);
        var (index1, index2) = (indices[0], indices[1]);

        if (index2 < index1)
        {
            (point1, point2) = (point2, point1);
            (index1, index2) = (index2, index1);
        }

        vertices1.Add(point1); vertices1.Add(point2);
        vertices2.Add(point1); vertices2.Add(point2);

        List<int> locTriangles1;
        List<Vector3> locVertices1;

        List<int> locTriangles2;
        List<Vector3> locVertices2;

        var trigInd = i + sideToInd[index1 + index2];

        if (vertexValues[mesh.triangles[trigInd]] > 0)
        {
            locTriangles1 = triangles1;
            locVertices1 = vertices1;

            locTriangles2 = triangles2;
            locVertices2 = vertices2;
        }
        else
        {
            locTriangles1 = triangles2;
            locVertices1 = vertices2;

            locTriangles2 = triangles1;
            locVertices2 = vertices1;
        }

        // triangle
        switch (index1 + index2)
        {
            case 3:  // 1, 2
                locTriangles1.Add(locVertices1.Count - 1);
                locTriangles1.Add(locVertices1.Count - 2);
                break;
            case 4:  // 1, 3
                locTriangles1.Add(locVertices1.Count - 2);
                locTriangles1.Add(locVertices1.Count - 1);
                break;
            case 5:  // 2, 3
                locTriangles1.Add(locVertices1.Count - 1);
                locTriangles1.Add(locVertices1.Count - 2);
                break;
        }

        AddVertex(mesh, locTriangles1, locVertices1, trigInd);

        // two triangles
        var (smol, bic) = (locVertices2.Count - 2, locVertices2.Count - 1);
        switch (index1 + index2)
        {
            case 3:  // 1, 2
                locTriangles2.Add(smol); locTriangles2.Add(bic);
                AddVertex(mesh, locTriangles2, locVertices2, i + 2);

                AddVertex(mesh, locTriangles2, locVertices2, i + 2);
                AddVertex(mesh, locTriangles2, locVertices2, i + 0);
                locTriangles2.Add(smol);
                break;
            case 4:  // 1, 3
                locTriangles2.Add(bic); locTriangles2.Add(smol);
                AddVertex(mesh, locTriangles2, locVertices2, i + 1);

                AddVertex(mesh, locTriangles2, locVertices2, i + 1);
                AddVertex(mesh, locTriangles2, locVertices2, i + 2);
                locTriangles2.Add(bic);
                break;
            case 5:  // 2, 3
                locTriangles2.Add(smol); locTriangles2.Add(bic);
                AddVertex(mesh, locTriangles2, locVertices2, i + 0);

                AddVertex(mesh, locTriangles2, locVertices2, i + 0);
                AddVertex(mesh, locTriangles2, locVertices2, i + 1);
                locTriangles2.Add(smol);
                break;
        }
    }

    (Mesh, Mesh) SliceMesh(Mesh mesh, Transform meshTransform)
    {
        // FillLineToTrig(mesh);

        lineToIntersection = new Dictionary<(int, int), Vector3>();

        var normal = Vector3.Cross(
            meshTransform.InverseTransformVector(slicer.up),
            meshTransform.InverseTransformVector(slicer.right));
        var planePoint = meshTransform.InverseTransformPoint(slicer.position);

        CaclulateVetrexValues(normal, planePoint, mesh.vertices);

        return CreateMeshes(mesh);

        /*foreach (var pair in lineToTrig)
        {
            var (ind1, ind2) = pair.Key;
            var (pt1, pt2) = (mesh.vertices[ind1], mesh.vertices[ind2]);

            var (succ, vec, scalar) = LinePlaneIntersection(
                normal, planePoint,
                pt2 - pt1, pt1);

            if (succ)
                lineToIntersection.Add(pair.Key, vec);
        }

        for (int ind = 0; ind < mesh.triangles.Length; ind += 3)
        {
            SliceTriangle(mesh, ind);
        }*/
    }

    (bool, Vector3, float) LinePlaneIntersectionByIndex(Vector3[] vertices, int i1, int i2)
    {
        var dt = vertexValues[i2] - vertexValues[i1];
        if (dt == 0)  // paralel
        {
            return (false, new Vector3(), 0);
        }
        else
        {
            var scalar = -vertexValues[i1] / dt;

            if (0 <= scalar && scalar <= 1)  // point on segment
                return (true, vertices[i1] + (vertices[i2] - vertices[i1]) * scalar, scalar);
            else  // point on line
                return (false, new Vector3(), scalar);
        }
    }

    (bool, Vector3, float) LinePlaneIntersection(Vector3 normal, Vector3 planePoint, Vector3 direction, Vector3 linePoint)
    {
        var dt = Vector3.Dot(direction, normal);
        if (dt == 0)  // paralel
        {
            return (false, new Vector3(), 0);
        }
        else
        {
            var scalar = Vector3.Dot(planePoint - linePoint, normal) / dt;

            if (0 <= scalar && scalar <= 1)  // point on segment
                return (true, linePoint + direction * scalar, scalar);
            else  // point on line
                return (false, new Vector3(), scalar);
        }
    }
}
