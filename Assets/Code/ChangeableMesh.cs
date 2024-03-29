﻿using System.Collections.Generic;
using UnityEngine;

public class ChangeableMesh
{
    public List<Vector3> vertices;
    public List<int> triangles;
    public List<Vector3> normals;

    public ChangeableMesh() : this(new List<Vector3>(), new List<int>(), new List<Vector3>())
    {
    }

    public ChangeableMesh(List<Vector3> vertices, List<int> triangles, List<Vector3> normals)
    {
        this.vertices = vertices;
        this.triangles = triangles;
        this.normals = normals;
    }

    public Mesh ToMesh()
    {
        var mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
