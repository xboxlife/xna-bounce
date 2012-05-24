// ========================================================================================== //
//                      ____                             _                                    //
//                     |  _ \                           | |                                   //
//                     | |_) | ___  _   _ _ __   ___ ___| |                                   //
//                     |  _ < / _ \| | | | '_ \ / __/ _ \ |                                   //
//                     | |_) | (_) | |_| | | | | (_|  __/_|                                   //
//                     |____/ \___/ \__,_|_| |_|\___\___(_)                                   //
//                                                                                            //
// ========================================================================================== //
// Developed and implemented by:                                                              //
//     Theodor Mader                                                                          //
//                                                                                            //
// Part of Portfolio projects, www.theomader.com                                              //
//                                                                                            //
// Copyright 2008. All rights reserved.                                                       //
// ========================================================================================== //

#region File Description
//-----------------------------------------------------------------------------//
// Class: PhysicsEngine.cs
// Author: Theodor Mader
//                                                                    
//  Level geometry.
// 
//  Stores vertices, normals and faces in arrays, also contains information about
//  min and max coordinate values in all coordinate directions. 
//  Additionally contains a vertex and index buffer which allows direct drawing
//  of the contained geometry
//-----------------------------------------------------------------------------//

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;


#endregion
namespace Bounce
{
    public class Face : IComparable<Face>
    {
        private uint vertexID1;
        public uint v1 { get { return vertexID1; } }

        private uint vertexID2;
        public uint v2 { get { return vertexID2; } }

        private uint vertexID3;
        public uint v3 { get { return vertexID3; } }

        private uint normalID1;
        public uint n1 { get { return normalID1; } }

        private uint normalID2;
        public uint n2 { get { return normalID2; } }

        private uint normalID3;
        public uint n3 { get { return normalID3; } }

        private Vector3 mFaceNormal;
        public Vector3 faceNormal { 
            get { return mFaceNormal; }
            set { mFaceNormal = value; }
        }



        public BoundingSphere boundingSphere;

        private uint ID;
        private static uint mNextID = 0;

        public Face( uint vertex1, uint vertex2, uint vertex3, uint normal1, uint normal2, uint normal3 )
        {
            vertexID1 = vertex1;
            vertexID2 = vertex2;
            vertexID3 = vertex3;

            normalID1 = normal1;
            normalID2 = normal2;
            normalID3 = normal3;

            ID = mNextID;
            mNextID++;

         }


        public int CompareTo( Face other)
        {
            return ID.CompareTo( other.ID );
        }


    }


    /// <summary>
    /// LevelGeometry holds the geometry referenced by the bsp tree. Following the definition of
    /// .obj files, it stores vertices and normals in arrays, and triangles reference into these arrays
    /// For debugging purposes, the class also allows the creation of a vertex and index buffer, and 
    /// provides a shader that can be used to draw the geometry.
    /// </summary>
    public class LevelGeometry
    {
        private Vector3[] mVertices;
        public Vector3[] vertices
        {
            get { return mVertices; }
        }

        private Vector3[] mNormals;
        public Vector3[] normals
        {
            get { return mNormals; }
        }

        private Face[] mFaces;
        public Face[] faces
        {
            get { return mFaces; }
        }


        // index and vertex buffer for direct drawing of geometry
        private VertexBuffer mVertexBuffer;
        public VertexBuffer vertexBuffer
        {
            get { return mVertexBuffer; }
        }

        private IndexBuffer mIndexBuffer;
        public IndexBuffer indexBuffer
        {
            get { return mIndexBuffer; }
        }

        // the vertex declaration we use for our vertex buffer
        private VertexDeclaration mVertexDeclaration;
        public VertexDeclaration vertexDeclaration
        {
            get { return mVertexDeclaration; }
        }

        // Effect to draw the level geometry
        private Effect mEffect;
        public Effect effect
        {
            get { return mEffect; }
        }


        // smalles x coordinate of all vertices
        private float mMinX;
        public float minX { get { return mMinX; } }

        // smallest y coordinate of all vertices
        private float mMinY;
        public float minY { get { return mMinY; } }

        // smallest z coordinate of all vertices
        private float mMinZ;
        public float minZ { get { return mMinZ; } }

        // biggest x coordinate of all vertices
        private float mMaxX;
        public float maxX { get { return mMaxX; } }

        // biggest y coordinate of all vertices
        private float mMaxY;
        public float maxY { get { return mMaxY; } }

        // biggest z coordinate of all vertices
        private float mMaxZ;
        public float maxZ { get { return mMaxZ; } }

        public LevelGeometry(Vector3[] vertices, Vector3[] normals, Face[] faces)
        {
            mVertices = vertices;
            mNormals = normals;
            mFaces = faces;

            calcBoundingBox();
        }

        // calculates the min/max coordinates in x, y and z direction
        private void calcBoundingBox()
        {

            mMinX = float.MaxValue;
            mMinY = float.MaxValue;
            mMinZ = float.MaxValue;
            mMaxX = float.MinValue;
            mMaxY = float.MinValue;
            mMaxZ = float.MinValue;


            foreach (Vector3 v in mVertices)
            {
                if (v.X < mMinX) mMinX = v.X;
                else if (v.X > mMaxX) mMaxX = v.X;

                if (v.Y < mMinY) mMinY = v.Y;
                else if (v.Y > mMaxY) mMaxY = v.Y;

                if (v.Z < mMinZ) mMinZ = v.Z;
                else if (v.Z > mMaxZ) mMaxZ = v.Z;
            }
        }

        /// <summary>
        /// Creates a new vertex and index buffer, fills buffers with data in vertices and faces
        /// </summary>
        /// <param name="contentManager"></param>
        /// <param name="graphicsDevice"></param>
        public void loadContent( ContentManager contentManager, GraphicsDevice graphicsDevice)
        {
            // set the vertex declaration
            mVertexDeclaration = VertexPositionNormalTexture.VertexDeclaration;

            // create new vertex buffer
            mVertexBuffer = new VertexBuffer(
                graphicsDevice,
                mVertexDeclaration,
                vertices.Length,
                BufferUsage.WriteOnly
            );

            // for each vertex with multiple normals: average them
            Dictionary<uint, Vector3> vertexToNormals = new Dictionary<uint, Vector3>();
            Dictionary<uint, uint> vertexNormalCount = new Dictionary<uint, uint>();


            foreach (Face f in faces)
            {
                if (!vertexToNormals.ContainsKey(f.v1))
                {
                    vertexToNormals.Add(f.v1, f.faceNormal);
                    vertexNormalCount.Add(f.v1, 1);
                }
                else
                {
                    vertexToNormals[f.v1] += f.faceNormal;
                    vertexNormalCount[f.v1]++;
                }

                if (!vertexToNormals.ContainsKey(f.v2))
                {
                    vertexToNormals.Add(f.v2, f.faceNormal);
                    vertexNormalCount.Add(f.v2, 1);
                }
                else
                {
                    vertexToNormals[f.v2] += f.faceNormal;
                    vertexNormalCount[f.v2]++;
                }

                if (!vertexToNormals.ContainsKey(f.v3))
                {
                    vertexToNormals.Add(f.v3, f.faceNormal);
                    vertexNormalCount.Add(f.v3, 1);
                }
                else
                {
                    vertexToNormals[f.v3] += f.faceNormal;
                    vertexNormalCount[f.v3]++;
                }
            }

            VertexPositionNormalTexture[] v = new VertexPositionNormalTexture[vertices.Length];
            for (uint vertexIdx = 0; vertexIdx < mVertices.Length; vertexIdx++)
            {
                v[vertexIdx] = new VertexPositionNormalTexture(
                    new Vector3(vertices[vertexIdx].X, vertices[vertexIdx].Y, vertices[vertexIdx].Z),
                    Vector3.Normalize(vertexToNormals[vertexIdx] / vertexNormalCount[vertexIdx]),
                    //f.normal,
                    new Vector2()
                );
            }

            // fill vertexbuffer
            vertexBuffer.SetData<VertexPositionNormalTexture>(v);


            // create index buffer
            uint i = 0;
            uint[] indices = new uint[mFaces.Length * 3];
            foreach (Face f in mFaces)
            {
                indices[i] = f.v1;
                indices[i + 1] = f.v2;
                indices[i + 2] = f.v3;
                i += 3;
            }

            mIndexBuffer = new IndexBuffer(graphicsDevice,IndexElementSize.ThirtyTwoBits, mFaces.Length * 3, BufferUsage.WriteOnly);

            indexBuffer.SetData<UInt32>(indices);

            // use BasicEffect to for drawing the geometry
            BasicEffect eff = new BasicEffect(graphicsDevice);
            eff.EnableDefaultLighting();
            eff.TextureEnabled = false;
            eff.DiffuseColor = new Vector3(0.8f, 0f, 0f);
            eff.EmissiveColor = new Vector3(0.2f, 0f, 0f);
            eff.Alpha = 0.25f;
            eff.PreferPerPixelLighting = true;
            eff.VertexColorEnabled = false;
            eff.DirectionalLight0.Enabled = true;
            eff.DirectionalLight1.Enabled = false;
            eff.DirectionalLight2.Enabled = false;
            eff.World = Matrix.Identity;

            eff.Parameters["World"].SetValue(Matrix.Identity);
            eff.Parameters["WorldInverseTranspose"].SetValue(Matrix.Identity);

            mEffect = eff;
        }
    }
}
