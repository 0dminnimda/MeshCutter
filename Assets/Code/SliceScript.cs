using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static System.Math;
using System;

public class SliceScript : MonoBehaviour
{
    [SerializeField]
    Transform slicer;

    [SerializeField]
    List<GameObject> slicees;

    Dictionary<(int, int), List<int>> lineToTrig = new Dictionary<(int, int), List<int>>();
    Dictionary<(int, int), Vector3> lineToIntersection = new Dictionary<(int, int), Vector3>();

    float[] vertexValues;

    bool thereIsAnIntersection = false;

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
                if (thereIsAnIntersection)
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

        if (thereIsAnIntersection)
        {
            var t = objTransform.parent;
            GameObject obj1, obj2;

            if (t == null)
            {
                obj1 = Instantiate(obj, obj.transform.position, Quaternion.identity);
                obj2 = Instantiate(obj, obj.transform.position, Quaternion.identity);
            }
            else
            {
                obj1 = Instantiate(obj, obj.transform.position, Quaternion.identity, t);
                obj2 = Instantiate(obj, obj.transform.position, Quaternion.identity, t);
            }

            obj1.GetComponent<MeshFilter>().mesh = mesh1;
            obj2.GetComponent<MeshFilter>().mesh = mesh2;

            slicees.Add(obj1);
            slicees.Add(obj2);

            Destroy(obj);
        }
    }

    (Mesh, Mesh) SliceMesh(Mesh mesh, Transform meshTransform)
    {
        lineToIntersection = new Dictionary<(int, int), Vector3>();

        var normal = slicer.forward;
        //meshTransform.InverseTransformVector(slicer.forward);
        /*Vector3.Cross(
        meshTransform.InverseTransformVector(slicer.up),
        meshTransform.InverseTransformVector(slicer.right));*/
        var planePoint = meshTransform.InverseTransformPoint(slicer.position);

        CaclulateVetrexValues(normal, planePoint, mesh.vertices);

        return CreateMeshes(mesh);
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

        lineToTrig = new Dictionary<(int, int), List<int>>();

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

                AddToLineToTrig(Sorted(i1, i2), i);
                AddToLineToTrig(Sorted(i1, i3), i);
                AddToLineToTrig(Sorted(i2, i3), i);
            }
        }

        AddIntercestion(mesh, cMesh1, cMesh2);

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

        var (succ, vec) = CachedLinePlaneIntersectionByIndex(mesh.vertices, i1, i2);
        if (succ) { points.Add(vec); indices.Add(1); }

        (succ, vec) = CachedLinePlaneIntersectionByIndex(mesh.vertices, i1, i3);
        if (succ) { points.Add(vec); indices.Add(3); }

        (succ, vec) = CachedLinePlaneIntersectionByIndex(mesh.vertices, i2, i3);
        if (succ) { points.Add(vec); indices.Add(2); }

        Debug.Assert(points.Count == 2);
        var (point1, point2) = (points[0], points[1]);

        Debug.Assert(indices.Count == 2);
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


    void AddIntercestion(Mesh mesh, ChangeableMesh cMesh1, ChangeableMesh cMesh2)
    {
        if (lineToIntersection.Count == 0)
        {
            Debug.Log("No intersection");
            thereIsAnIntersection = false;
            return;
        }

        thereIsAnIntersection = true;

        var lineToIntersectionClone = lineToIntersection.ToDictionary(
            entry => entry.Key, entry => entry.Value);

        var lines = new List<(int, int)>(lineToIntersectionClone.Count);
        var key = SetKey(lineToIntersection.ElementAt(0).Key, lines, lineToIntersectionClone);

        // get sorted intersection points
        while (lineToIntersectionClone.Count != 0)
        {
            // get trigInd
            var trigInd = NewTriangle(key, lines, lineToIntersectionClone);
            if (trigInd == -1)
            {
                Debug.LogError("Shold be unreachable, trigInd wasn't set");
                throw new InvalidOperationException();
            }

            // get indices
            var i1 = mesh.triangles[trigInd + 0];
            var i2 = mesh.triangles[trigInd + 1];
            var i3 = mesh.triangles[trigInd + 2];

            (int, int)[] keys = { Sorted(i1, i2), Sorted(i1, i3), Sorted(i2, i3) };

            // gather new key
            var gathered = false;
            foreach (var potentialKey in keys)
            {
                if (lineToIntersectionClone.ContainsKey(potentialKey))
                {
                    if (gathered)
                    {
                        Debug.LogError("Second key in the triangle");
                    }
                    else
                    {
                        key = SetKey(potentialKey, lines, lineToIntersectionClone);
                        gathered = true;
                    }
                }
            }

            // if (!gathered) should be roselved on the next stage when getting trigInd
            /*if (!gathered)
            {
                Debug.LogError("Shold be unreachable, no new key gathered");
                throw new InvalidOperationException();
            }*/


        }

        // world Vector3 -> plane Vector2
        var points2d = new List<Vector2>(lines.Count);
        var vertices = new List<Vector3>(lines.Count);
        foreach (var line in lines)
        {
            var point = lineToIntersection[line];
            points2d.Add(new Vector2(
                Vector3.Dot(point, slicer.right),
                Vector3.Dot(point, slicer.up)));
            vertices.Add(point);
        }

        // Use the triangulator to get indices for creating triangles
        List<int> _indices = new List<int>();
        var r = Triangulator.Triangulate(points2d, _indices);
        if (!r)
            Debug.LogError("Error");

        int[] indices = _indices.ToArray();

        // add cross-section to mesh
        var baseInd = cMesh2.vertices.Count;
        foreach (var item in indices)
        {
            cMesh2.triangles.Add(item + baseInd);
        }
        cMesh2.vertices.AddRange(vertices);

        baseInd = cMesh1.vertices.Count;
        foreach (var item in indices.Reverse())
        {
            cMesh1.triangles.Add(item + baseInd);
        }
        cMesh1.vertices.AddRange(vertices);
    }

    (int, int) SetKey((int, int) key, List<(int, int)> lines,
                      Dictionary<(int, int), Vector3> lineToIntersectionClone)
    {
        lines.Add(key);
        lineToIntersectionClone.Remove(key);
        return key;
    }

    int NewTriangle((int, int) key, List<(int, int)> lines,
                    Dictionary<(int, int), Vector3> lineToIntersectionClone)
    {
        List<int> lst;
        var trigInd = -1;

        if (lineToTrig.TryGetValue(key, out lst))
        {
            trigInd = lst[0];
            if (lst.Count == 1)
                lineToTrig.Remove(key);
            else
                lst.RemoveAt(0);
        }
        else
        {
            key = SetKey(FindClosestPoint(key, lineToIntersectionClone),
                         lines, lineToIntersectionClone);

            if (lineToTrig.TryGetValue(key, out lst))
            {
                trigInd = lst[0];
                if (lst.Count == 1)
                    lineToTrig.Remove(key);
                else
                    lst.RemoveAt(0);
            }
            else
            {
                Debug.LogError("Shold be unreachable, new key don't have any " +
                               "connected triangles left, it was triangleless already");
                throw new InvalidOperationException();
            }
            // Debug.LogError("Shold be unreachable, new key don't have any connected triangles left");
        }

        return trigInd;
    }

    (int, int) FindClosestPoint((int, int) key, Dictionary<(int, int), Vector3> lineToIntersectionClone)
    {
        var closest = (-1, -1);
        if (lineToIntersectionClone.Count < 1)
        {
            Debug.LogError("lineToIntersection don't have enough elements (at least one)");
            return closest;
        }

        // XXX: key should be in lineToIntersection, but could not be
        var point = lineToIntersection[key];

        var dist = float.PositiveInfinity;
        float tmpDist;

        foreach (var pair in lineToIntersectionClone)
        {
            if (pair.Key != key)
            {
                tmpDist = Vector3.Distance(point, pair.Value);
                if (tmpDist < dist)
                {
                    closest = pair.Key;
                    dist = tmpDist;
                }
            }
        }

        if (closest == (-1, -1))
        {
            Debug.LogError("Closest isn't set in the end");
        }

        return closest;
    }


    (bool, Vector3) CachedLinePlaneIntersectionByIndex(Vector3[] vertices, int i1, int i2)
    {
        var key = Sorted(i1, i2);
        Vector3 val;

        if (!lineToIntersection.TryGetValue(key, out val))
        {
            var (succ, vec) = LinePlaneIntersectionByIndex(vertices, i1, i2);
            if (succ)
                lineToIntersection.Add(key, vec);
            return (succ, vec);
        }
        return (true, val);
    }

    (bool, Vector3) LinePlaneIntersectionByIndex(Vector3[] vertices, int i1, int i2)
    {
        var dt = vertexValues[i2] - vertexValues[i1];
        if (dt == 0)  // paralel
        {
            return (false, new Vector3());
        }
        else
        {
            var scalar = -vertexValues[i1] / dt;

            if (0 <= scalar && scalar <= 1)  // point on segment
                return (true, vertices[i1] + (vertices[i2] - vertices[i1]) * scalar);
            else  // point on line
                return (false, new Vector3());
        }
    }

    (bool, Vector3) LinePlaneIntersection(Vector3 normal, Vector3 planePoint,
                                          Vector3 direction, Vector3 linePoint)
    {
        var dt = Vector3.Dot(direction, normal);
        if (dt == 0)  // paralel
        {
            return (false, new Vector3());
        }
        else
        {
            var scalar = Vector3.Dot(planePoint - linePoint, normal) / dt;

            if (0 <= scalar && scalar <= 1)  // point on segment
                return (true, linePoint + direction * scalar);
            else  // point on line
                return (false, new Vector3());
        }
    }
}
