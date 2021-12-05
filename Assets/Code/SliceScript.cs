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
    List<GameObject> slicees;

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
        var indicesToRemove = new List<int>();

        for (int i = slicees.Count - 1; i >= 0; i--)
        {
            var obj = slicees[i];
            var mf = obj.GetComponent<MeshFilter>();
            var t = obj.GetComponent<Transform>();

            if (mf != null && t != null)
            {
                SliceGameObject(obj, t, mf.mesh);
                indicesToRemove.Add(i);
            }
        }

        foreach (var i in indicesToRemove)
        {
            slicees.RemoveAt(i);
        }
    }

    void SliceGameObject(GameObject obj, Transform objTransform, Mesh objMesh)
    {
        var (mesh1, mesh2) = SliceMesh(objMesh, objTransform);
        var t = obj.GetComponentInParent<Transform>();

        GameObject obj1, obj2;

        if (t == null || t.gameObject == obj)
        {
            obj1 = Instantiate(obj, obj.transform.position, Quaternion.identity);
            obj1.GetComponent<MeshFilter>().mesh = mesh1;

            obj2 = Instantiate(obj, obj.transform.position, Quaternion.identity);
            obj2.GetComponent<MeshFilter>().mesh = mesh2;
        }
        else
        {
            obj1 = Instantiate(obj, obj.transform.position, Quaternion.identity, t);
            obj1.GetComponent<MeshFilter>().mesh = mesh1;

            obj2 = Instantiate(obj, obj.transform.position, Quaternion.identity, t);
            obj2.GetComponent<MeshFilter>().mesh = mesh2;
        }

        slicees.Add(obj1);
        slicees.Add(obj2);

        Destroy(obj);
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

    (int, int) Sorted(int a, int b)
    {
        return (Min(a, b), Max(a, b));
    }


    void CaclulateVetrexValues(Vector3 normal, Vector3 planePoint, Vector3[] vertices)
    {
        vertexValues = new float[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            vertexValues[i] = Vector3.Dot(vertices[i] - planePoint, normal);
        }
    }


    (Mesh, Mesh) CreateMeshes(Mesh mesh)
    {
        var cMesh1 = new ChangeableMesh();
        var cMesh2 = new ChangeableMesh();

        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            var i1 = mesh.triangles[i + 0];
            var i2 = mesh.triangles[i + 1];
            var i3 = mesh.triangles[i + 2];

            if (vertexValues[i1] > 0 && vertexValues[i2] > 0 && vertexValues[i3] > 0)
            {
                AddTriangle(mesh, cMesh1, i1, i2, i3);
            }
            else if (vertexValues[i1] < 0 && vertexValues[i2] < 0 && vertexValues[i3] < 0)
            {
                AddTriangle(mesh, cMesh2, i1, i2, i3);
            }
            else  // triangle is intersected
            {
                SliceTriangle(mesh, i, i1, i2, i3, cMesh1, cMesh2);
            }
        }

        return (cMesh1.ToMesh(), cMesh2.ToMesh());
    }

    void AddTriangle(Mesh mesh, ChangeableMesh cMesh, int i1, int i2, int i3)
    {
        cMesh.vertices.Add(mesh.vertices[i1]);
        cMesh.vertices.Add(mesh.vertices[i2]);
        cMesh.vertices.Add(mesh.vertices[i3]);

        cMesh.triangles.Add(cMesh.vertices.Count - 3);
        cMesh.triangles.Add(cMesh.vertices.Count - 2);
        cMesh.triangles.Add(cMesh.vertices.Count - 1);
    }

    void SliceTriangle(Mesh mesh, int i, int i1, int i2, int i3,
                       ChangeableMesh cMesh1, ChangeableMesh cMesh2)
    {
        var points = new List<Vector3>(2);
        var indices = new List<int>(2);

        var (succ, vec, scalar) = LinePlaneIntersectionByIndex(mesh.vertices, i1, i2);
        if (succ) { points.Add(vec); indices.Add(1); }
        // if (succ) lineToIntersection.Add(pair.Key, vec);

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

        cMesh1.vertices.Add(point1); cMesh1.vertices.Add(point2);
        cMesh2.vertices.Add(point1); cMesh2.vertices.Add(point2);

        var trigInd = i + sideToInd[index1 + index2];

        var locCMesh1 = cMesh1; var locCMesh2 = cMesh2;
        if (vertexValues[mesh.triangles[trigInd]] < 0)
            (locCMesh1, locCMesh2) = (locCMesh2, locCMesh1);

        // one triangle
        var (smol, bic) = (locCMesh1.vertices.Count - 2, locCMesh1.vertices.Count - 1);
        switch (index1 + index2)
        {
            case 3:  // 1, 2
                locCMesh1.triangles.Add(bic);
                locCMesh1.triangles.Add(smol);
                break;
            case 4:  // 1, 3
                locCMesh1.triangles.Add(smol);
                locCMesh1.triangles.Add(bic);
                break;
            case 5:  // 2, 3
                locCMesh1.triangles.Add(bic);
                locCMesh1.triangles.Add(smol);
                break;
        }
        AddVertex(mesh, locCMesh1, trigInd);

        // two triangles
        (smol, bic) = (locCMesh2.vertices.Count - 2, locCMesh2.vertices.Count - 1);
        switch (index1 + index2)
        {
            case 3:  // 1, 2
                locCMesh2.triangles.Add(smol);
                locCMesh2.triangles.Add(bic);
                AddVertex(mesh, locCMesh2, i + 2);

                AddVertex(mesh, locCMesh2, i + 2);
                AddVertex(mesh, locCMesh2, i + 0);
                locCMesh2.triangles.Add(smol);
                break;
            case 4:  // 1, 3
                locCMesh2.triangles.Add(bic);
                locCMesh2.triangles.Add(smol);
                AddVertex(mesh, locCMesh2, i + 1);

                AddVertex(mesh, locCMesh2, i + 1);
                AddVertex(mesh, locCMesh2, i + 2);
                locCMesh2.triangles.Add(bic);
                break;
            case 5:  // 2, 3
                locCMesh2.triangles.Add(smol);
                locCMesh2.triangles.Add(bic);
                AddVertex(mesh, locCMesh2, i + 0);

                AddVertex(mesh, locCMesh2, i + 0);
                AddVertex(mesh, locCMesh2, i + 1);
                locCMesh2.triangles.Add(smol);
                break;
        }
    }

    void AddVertex(Mesh mesh, ChangeableMesh cMesh, int i)
    {
        var vertex = mesh.vertices[mesh.triangles[i]];
        var ind = cMesh.vertices.IndexOf(vertex);
        if (ind != -1)
            cMesh.triangles.Add(ind);
        else
        {
            cMesh.vertices.Add(vertex);
            cMesh.triangles.Add(cMesh.vertices.Count - 1);
        }
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
