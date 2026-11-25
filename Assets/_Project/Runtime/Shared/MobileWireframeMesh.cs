using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MRStrategyRTS.Shared
{
    /// <summary>
    /// Supplies per-triangle barycentric vertex colors for the mobile wireframe shader.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileWireframeMesh : MonoBehaviour
    {
        private const string WireframeShaderName = "Custom/Wireframe_GS";

        private readonly List<Mesh> generatedMeshes = new();

        private void Awake()
        {
            BuildBarycentricMeshes();
        }

        private void OnDestroy()
        {
            foreach (var mesh in generatedMeshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }
        }

        private void BuildBarycentricMeshes()
        {
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                var source = filter.sharedMesh;
                if (renderer == null || source == null || !UsesWireframeMaterial(renderer.sharedMaterials))
                {
                    continue;
                }

                var barycentricMesh = CreateBarycentricMesh(source);
                filter.sharedMesh = barycentricMesh;
                generatedMeshes.Add(barycentricMesh);
            }
        }

        private static bool UsesWireframeMaterial(Material[] materials)
        {
            foreach (var material in materials)
            {
                if (material != null && material.shader != null &&
                    material.shader.name == WireframeShaderName)
                {
                    return true;
                }
            }

            return false;
        }

        private static Mesh CreateBarycentricMesh(Mesh source)
        {
            var sourceVertices = source.vertices;
            var sourceNormals = source.normals;
            var sourceTangents = source.tangents;
            var sourceUv = source.uv;
            var sourceUv2 = source.uv2;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uv = new List<Vector2>();
            var uv2 = new List<Vector2>();
            var colors = new List<Color>();
            var submeshIndices = new List<int[]>();

            for (var submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                if (source.GetTopology(submesh) != MeshTopology.Triangles)
                {
                    submeshIndices.Add(null);
                    continue;
                }

                var sourceIndices = source.GetIndices(submesh);
                var indices = new int[sourceIndices.Length];
                for (var index = 0; index < sourceIndices.Length; index++)
                {
                    var sourceIndex = sourceIndices[index];
                    indices[index] = vertices.Count;
                    vertices.Add(sourceVertices[sourceIndex]);
                    if (sourceNormals.Length > sourceIndex) normals.Add(sourceNormals[sourceIndex]);
                    if (sourceTangents.Length > sourceIndex) tangents.Add(sourceTangents[sourceIndex]);
                    if (sourceUv.Length > sourceIndex) uv.Add(sourceUv[sourceIndex]);
                    if (sourceUv2.Length > sourceIndex) uv2.Add(sourceUv2[sourceIndex]);
                    colors.Add(index % 3 == 0 ? Color.red : index % 3 == 1 ? Color.green : Color.blue);
                }

                submeshIndices.Add(indices);
            }

            var mesh = new Mesh
            {
                name = $"{source.name} (Mobile Wireframe)"
            };
            if (vertices.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count) mesh.SetNormals(normals);
            if (tangents.Count == vertices.Count) mesh.SetTangents(tangents);
            if (uv.Count == vertices.Count) mesh.SetUVs(0, uv);
            if (uv2.Count == vertices.Count) mesh.SetUVs(1, uv2);
            mesh.SetColors(colors);
            mesh.subMeshCount = source.subMeshCount;
            for (var submesh = 0; submesh < submeshIndices.Count; submesh++)
            {
                if (submeshIndices[submesh] != null)
                {
                    mesh.SetIndices(submeshIndices[submesh], MeshTopology.Triangles, submesh);
                }
            }

            mesh.bounds = source.bounds;
            return mesh;
        }
    }
}
