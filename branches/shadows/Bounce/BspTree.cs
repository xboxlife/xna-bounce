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
// Class: BspTree.cs
// Author: Theodor Mader
//                                                            
// 
//-----------------------------------------------------------------------------//
#endregion


using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace Bounce
{

    /// <summary>
    /// Node for the BSP tree. Contains a separating plane and reference to the 
    /// nodes on the positive/negative side of the plane. Note: leaf nodes are 
    /// distinguished by the separating plane (0,0,0,0). They contain a list of 
    /// faces
    /// </summary>
    class BspNode
    {
        public Plane separatingPlane;
        public BspNode pos, neg;
        public List<Face> faces = null;

        public BspNode(Plane separatingPlane, List<Face> faces)
        {
            this.separatingPlane = separatingPlane;
            this.faces = faces;
        }

        public BspNode(Plane separatingPlane)
        {
            this.separatingPlane = separatingPlane;
            faces = null;
        }

        public BspNode()
        {
            separatingPlane = new Plane(0, 0, 0, 0);
        }
    }

    /// <summary>
    /// Bsp tree used for collision detection. After reading the separating planes from a binary
    /// file, collisions with spheres can be computed by collisions()
    /// </summary>
    public class BspTree
    {
        private BspNode mRoot;  // root node

        // A separate copy of the level geometry
        private LevelGeometry mLevelData = null;    
        public LevelGeometry levelGeometry
        {
            get { return mLevelData; }
        }

        /// <summary>
        /// Read the tree data from a file and create it. 
        /// </summary>
        /// <param name="file">File to read the bsp tree from</param>
        public void readFromFile(string file)
        {

            BinaryReader binReader =
                new BinaryReader(File.Open(file, FileMode.Open));

            // read the number of separating planes first
            uint numPlanes = binReader.ReadUInt32();

            // call recursive tree building procedure
            buildTree(ref mRoot, binReader, ref numPlanes);

            // now read the level geometry

            // vertices first
            uint numVertices = binReader.ReadUInt32();
            Vector3[] vertices = new Vector3[numVertices];
            for (int i = 0; i < numVertices; i++)
            {
                vertices[i].X = binReader.ReadSingle();
                vertices[i].Y = binReader.ReadSingle();
                vertices[i].Z = binReader.ReadSingle();
            }

            // read the normals
            uint numNormals = binReader.ReadUInt32();
            Vector3[] normals = new Vector3[numNormals];
            for (int i = 0; i < numNormals; i++)
            {
                normals[i].X = binReader.ReadSingle();
                normals[i].Y = binReader.ReadSingle();
                normals[i].Z = binReader.ReadSingle();
            }

            // read the faces
            uint numFaces = binReader.ReadUInt32();
            Face[] faces = new Face[numFaces];

            for (int i = 0; i < numFaces; i++)
            {
                
                uint v1 = binReader.ReadUInt32() - 1;
                uint n1 = binReader.ReadUInt32() - 1;
                uint v2 = binReader.ReadUInt32() - 1;
                uint n2 = binReader.ReadUInt32() - 1;
                uint v3 = binReader.ReadUInt32() - 1;
                uint n3 = binReader.ReadUInt32() - 1;

                Face f = new Face( v1, v2, v3, n1, n2, n3 );
                faces[i] = f;

                // compute bounding sphere for each face
                Vector3 center = vertices[f.v1] + vertices[f.v2] + vertices[f.v3];
                center /= 3;

                float radius = (center - vertices[f.v1]).Length();
                float dist2 = (center - vertices[f.v2]).Length();
                if (radius < dist2) radius = dist2;

                float dist3 = (center - vertices[f.v3]).Length();
                if (radius < dist3) radius = dist3;

                faces[i].boundingSphere = new BoundingSphere(center, radius);

                // compute face normal
                faces[i].faceNormal = Vector3.Cross(
                    vertices[f.v2] - vertices[f.v1],
                    vertices[f.v3] - vertices[f.v1]
                );
                faces[i].faceNormal.Normalize();
            }

            // create level geometry
            mLevelData = new LevelGeometry(vertices, normals, faces);

            // close binary file
            binReader.Close();

            // insert level geometry into tree leafs
            insertGeometryData();
        }


        /// <summary>
        /// Recursive tree building procedure: reads separating planes from the file, builds up
        /// the tree in preorder. 
        /// </summary>
        /// <param name="node">Node we are currently processing</param>
        /// <param name="file">Binary file to read separating planes from</param>
        /// <param name="numPlanesRemaining">Number of planes remaining</param>
        private void buildTree(ref BspNode node, BinaryReader file, ref uint numPlanesRemaining)
        {
            // no more planes remaining? append leaf node, stop recursion.
            if (numPlanesRemaining == 0)
            {
                node = new BspNode();
                node.faces = new List<Face>();
                return;
            }

            // read separating plane data
            double x = file.ReadDouble();
            double y = file.ReadDouble();
            double z = file.ReadDouble();
            double d = file.ReadDouble();

            // one plane less to read
            numPlanesRemaining--;

            // did we reach a leaf? 
            // in this case: create leaf node and stop this recursion on this branch
            if (x == 0.0 && y == 0.0 && z == 0.0 && d == 0.0)
            {
                node = new BspNode();
                node.faces = new List<Face>();
                return;
            }

            // neither finished, nor leaf reached: create a new node, start two new
            // recursions to the left and right of the tree
            node = new BspNode(new Plane((float)x, (float)y, (float)z, (float)d));
            buildTree(ref node.pos, file, ref numPlanesRemaining);
            buildTree(ref node.neg, file, ref numPlanesRemaining);
        }

        /// <summary>
        /// Inserts the faces in the level geometry into the leafs of the tree. If a face 
        /// intersects a separating plane,it is passed down on both sides of the plane.
        /// </summary>
        private void insertGeometryData()
        {
            // for each face
            foreach (Face face in mLevelData.faces)
            {
                Vector3 pos1 = mLevelData.vertices[face.v1];
                Vector3 pos2 = mLevelData.vertices[face.v2];
                Vector3 pos3 = mLevelData.vertices[face.v3];

                // create a list of nodes we have to process on our way to the leafs of the tree
                LinkedList<BspNode> toProcess = new LinkedList<BspNode>();
                toProcess.AddFirst(mRoot);

                // as long as we have nodes to process
                while (toProcess.Count > 0)
                {
                    // take first node
                    BspNode curNode = toProcess.First.Value;
                    toProcess.RemoveFirst();

                    // not leaf? propagate face to correct child node. (both nodes in case of intersection)
                    if (curNode.separatingPlane.Normal.X != 0.0f ||
                            curNode.separatingPlane.Normal.Y != 0.0f ||
                            curNode.separatingPlane.Normal.Z != 0.0f ||
                            curNode.separatingPlane.D != 0.0f)
                    {

                        // compute side each triangle vertex lies on
                        float side1 = Vector3.Dot(curNode.separatingPlane.Normal, pos1) + curNode.separatingPlane.D;
                        float side2 = Vector3.Dot(curNode.separatingPlane.Normal, pos2) + curNode.separatingPlane.D;
                        float side3 = Vector3.Dot(curNode.separatingPlane.Normal, pos3) + curNode.separatingPlane.D;

                        
                        if (side1 > 0 && side2 > 0 && side3 > 0)
                        {
                            // all points on positive side? propagate to positive side
                            toProcess.AddLast(curNode.pos);
                        }
                        else if (side1 <= 0 && side2 <= 0 && side3 <= 0)
                        {
                            // all points on negative side?  propagate to negative side
                            toProcess.AddLast(curNode.neg);
                        }
                        else
                        {
                            // no luck, triangle intersects plane, we have to propagate it on both sides
                            toProcess.AddLast(curNode.pos);
                            toProcess.AddLast(curNode.neg);
                        }
                    }
                    else
                        curNode.faces.Add(face);
                }
            }
        }

        /// <summary>
        /// Computes all triangles colliding with the given bounding sphere
        /// </summary>
        /// <param name="b">Bounding sphere to be checked for collision</param>
        /// <param name="collidingFaces">Colliding faces are added to this list (may contain duplicates!)</param>
        /// <param name="collisionPoints">For each colliding face, the exact collision points is added to this list</param>
        public void collisions(BoundingSphere b, LinkedList<Face> collidingFaces, LinkedList<Vector3> collisionPoints)
        {
            LinkedList<BspNode> toProcess = new LinkedList<BspNode>();
            toProcess.AddLast(mRoot);

            while (toProcess.Count > 0)
            {
                BspNode curNode = toProcess.First.Value;
                toProcess.RemoveFirst();

                // have we reached a leaf? check all triangles in leaf for collisions
                if (curNode.separatingPlane.Normal.X == 0.0f &&
                    curNode.separatingPlane.Normal.Y == 0.0f &&
                    curNode.separatingPlane.Normal.Z == 0.0f &&
                    curNode.separatingPlane.D == 0.0f)
                {
                    nodeCollisions(b, curNode, collidingFaces, collisionPoints);
                }

                else
                {
                    // propagate bounding sphere down the tree
                    PlaneIntersectionType side = curNode.separatingPlane.Intersects(b);

                    if (side == PlaneIntersectionType.Back) toProcess.AddLast(curNode.neg);
                    else if (side == PlaneIntersectionType.Front) toProcess.AddLast(curNode.pos);
                    else
                    {
                        toProcess.AddLast(curNode.pos);
                        toProcess.AddLast(curNode.neg);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the raw number of triangles actually checked for collision 
        /// (all triangles in all nodes that are reached)
        /// </summary>
        /// <param name="b"></param>
        /// <param name="result"></param>
        public void checkedFaces(BoundingSphere b, LinkedList<Face> result)
        {
            LinkedList<BspNode> toProcess = new LinkedList<BspNode>();
            toProcess.AddLast(mRoot);

            while (toProcess.Count > 0)
            {
                BspNode curNode = toProcess.First.Value;
                toProcess.RemoveFirst();

                if (curNode.separatingPlane.Normal.X == 0.0f &&
                    curNode.separatingPlane.Normal.Y == 0.0f &&
                    curNode.separatingPlane.Normal.Z == 0.0f &&
                    curNode.separatingPlane.D == 0.0f)
                {
                    foreach (Face f in curNode.faces)
                        result.AddLast(f);

                }

                else
                {
                    PlaneIntersectionType side = curNode.separatingPlane.Intersects(b);

                    if (side == PlaneIntersectionType.Back) toProcess.AddLast(curNode.neg);
                    else if (side == PlaneIntersectionType.Front) toProcess.AddLast(curNode.pos);
                    else
                    {
                        toProcess.AddLast(curNode.pos);
                        toProcess.AddLast(curNode.neg);
                    }
                }
            }
        }

        /// <summary>
        ///  Adds all triangles of node which intersect b to list result, returns exact intersection points in 
        /// intersectionPoints. the function closestPointInTriangle() is used for computing triangle-sphere
        /// intersections.
        /// </summary>
        /// <param name="b"></param>
        /// <param name="node"></param>
        /// <param name="result"></param>
        private void nodeCollisions(BoundingSphere b, BspNode node, LinkedList<Face> result, LinkedList<Vector3> intersectionPoints)
        {
            float squaredBoundingSphereRadius = b.Radius * b.Radius;

            foreach (Face face in node.faces)
            {
                Vector3 pos1, pos2, pos3;
                pos1 = mLevelData.vertices[face.v1];
                pos2 = mLevelData.vertices[face.v2];
                pos3 = mLevelData.vertices[face.v3];

                // check collision with triangle bounding sphere first
                if (b.Intersects(face.boundingSphere) == false)
                    continue;

                Vector3 closestPoint = closestPointInTriangle(b.Center, pos1, pos2, pos3);
                float squaredDist = (closestPoint - b.Center).LengthSquared();
                if (squaredDist <= squaredBoundingSphereRadius)
                {
                    result.AddLast(face);
                    intersectionPoints.AddLast(closestPoint);
                }

            }
        }

        /// <summary>
        /// Computes the distance between point and the triangle (v0, v1, v2). 
        /// Code taken from http://www.geometrictools.com/LibFoundation/Distance/Distance.html
        /// </summary>
        /// <param name="point"></param>
        /// <param name="v0"></param>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        private Vector3 closestPointInTriangle(Vector3 point, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            {
                //  Vector3<Real> kDiff = m_rkTriangle.V[0] - m_rkVector;
                Vector3 kDiff = v0 - point;
                Vector3 kEdge0 = v1 - v0;
                Vector3 kEdge1 = v2 - v0;

                double fA00 = kEdge0.LengthSquared();

                double fA01 = Vector3.Dot(kEdge0, kEdge1);
                double fA11 = kEdge1.LengthSquared();
                double fB0 = Vector3.Dot(kDiff, kEdge0);
                double fB1 = Vector3.Dot(kDiff, kEdge1);
                double fC = kDiff.LengthSquared();
                double fDet = Math.Abs(fA00 * fA11 - fA01 * fA01);
                double fS = fA01 * fB1 - fA11 * fB0;
                double fT = fA01 * fB0 - fA00 * fB1;
                double fSqrDistance;

                if (fS + fT <= fDet)
                {
                    if (fS < 0.0)
                    {
                        if (fT < 0.0)  // region 4
                        {
                            if (fB0 < 0.0)
                            {
                                fT = 0.0;
                                if (-fB0 >= fA00)
                                {
                                    fS = 1.0;
                                    fSqrDistance = fA00 + 2.0 * fB0 + fC;
                                }
                                else
                                {
                                    fS = -fB0 / fA00;
                                    fSqrDistance = fB0 * fS + fC;
                                }
                            }
                            else
                            {
                                fS = 0.0;
                                if (fB1 >= 0.0)
                                {
                                    fT = 0.0;
                                    fSqrDistance = fC;
                                }
                                else if (-fB1 >= fA11)
                                {
                                    fT = 1.0;
                                    fSqrDistance = fA11 + 2.0 * fB1 + fC;
                                }
                                else
                                {
                                    fT = -fB1 / fA11;
                                    fSqrDistance = fB1 * fT + fC;
                                }
                            }
                        }
                        else  // region 3
                        {
                            fS = 0.0;
                            if (fB1 >= 0.0)
                            {
                                fT = 0.0;
                                fSqrDistance = fC;
                            }
                            else if (-fB1 >= fA11)
                            {
                                fT = 1.0;
                                fSqrDistance = fA11 + 2.0 * fB1 + fC;
                            }
                            else
                            {
                                fT = -fB1 / fA11;
                                fSqrDistance = fB1 * fT + fC;
                            }
                        }
                    }
                    else if (fT < 0.0)  // region 5
                    {
                        fT = 0.0;
                        if (fB0 >= 0.0)
                        {
                            fS = 0.0;
                            fSqrDistance = fC;
                        }
                        else if (-fB0 >= fA00)
                        {
                            fS = 1.0;
                            fSqrDistance = fA00 + 2.0 * fB0 + fC;
                        }
                        else
                        {
                            fS = -fB0 / fA00;
                            fSqrDistance = fB0 * fS + fC;
                        }
                    }
                    else  // region 0
                    {
                        // minimum at interior point
                        double fInvDet = 1.0 / fDet;
                        fS *= fInvDet;
                        fT *= fInvDet;
                        fSqrDistance = fS * (fA00 * fS + fA01 * fT + 2.0 * fB0) +
                            fT * (fA01 * fS + fA11 * fT + 2.0 * fB1) + fC;
                    }
                }
                else
                {
                    double fTmp0, fTmp1, fNumer, fDenom;

                    if (fS < 0.0)  // region 2
                    {
                        fTmp0 = fA01 + fB0;
                        fTmp1 = fA11 + fB1;
                        if (fTmp1 > fTmp0)
                        {
                            fNumer = fTmp1 - fTmp0;
                            fDenom = fA00 - 2.0f * fA01 + fA11;
                            if (fNumer >= fDenom)
                            {
                                fS = 1.0;
                                fT = 0.0;
                                fSqrDistance = fA00 + 2.0 * fB0 + fC;
                            }
                            else
                            {
                                fS = fNumer / fDenom;
                                fT = 1.0 - fS;
                                fSqrDistance = fS * (fA00 * fS + fA01 * fT + 2.0f * fB0) +
                                    fT * (fA01 * fS + fA11 * fT + 2.0) * fB1 + fC;
                            }
                        }
                        else
                        {
                            fS = 0.0;
                            if (fTmp1 <= 0.0)
                            {
                                fT = 1.0;
                                fSqrDistance = fA11 + 2.0 * fB1 + fC;
                            }
                            else if (fB1 >= 0.0)
                            {
                                fT = 0.0;
                                fSqrDistance = fC;
                            }
                            else
                            {
                                fT = -fB1 / fA11;
                                fSqrDistance = fB1 * fT + fC;
                            }
                        }
                    }
                    else if (fT < 0.0)  // region 6
                    {
                        fTmp0 = fA01 + fB1;
                        fTmp1 = fA00 + fB0;
                        if (fTmp1 > fTmp0)
                        {
                            fNumer = fTmp1 - fTmp0;
                            fDenom = fA00 - 2.0 * fA01 + fA11;
                            if (fNumer >= fDenom)
                            {
                                fT = 1.0;
                                fS = 0.0;
                                fSqrDistance = fA11 + 2.0 * fB1 + fC;
                            }
                            else
                            {
                                fT = fNumer / fDenom;
                                fS = 1.0 - fT;
                                fSqrDistance = fS * (fA00 * fS + fA01 * fT + 2.0 * fB0) +
                                    fT * (fA01 * fS + fA11 * fT + 2.0 * fB1) + fC;
                            }
                        }
                        else
                        {
                            fT = 0.0;
                            if (fTmp1 <= 0.0)
                            {
                                fS = 1.0;
                                fSqrDistance = fA00 + 2.0 * fB0 + fC;
                            }
                            else if (fB0 >= 0.0)
                            {
                                fS = 0.0;
                                fSqrDistance = fC;
                            }
                            else
                            {
                                fS = -fB0 / fA00;
                                fSqrDistance = fB0 * fS + fC;
                            }
                        }
                    }
                    else  // region 1
                    {
                        fNumer = fA11 + fB1 - fA01 - fB0;
                        if (fNumer <= 0.0)
                        {
                            fS = 0.0;
                            fT = 1.0;
                            fSqrDistance = fA11 + 2.0 * fB1 + fC;
                        }
                        else
                        {
                            fDenom = fA00 - 2.0f * fA01 + fA11;
                            if (fNumer >= fDenom)
                            {
                                fS = 1.0;
                                fT = 0.0;
                                fSqrDistance = fA00 + 2.0 * fB0 + fC;
                            }
                            else
                            {
                                fS = fNumer / fDenom;
                                fT = 1.0 - fS;
                                fSqrDistance = fS * (fA00 * fS + fA01 * fT + 2.0 * fB0) +
                                    fT * (fA01 * fS + fA11 * fT + 2.0 * fB1) + fC;
                            }
                        }
                    }
                }

                // account for numerical round-off error
                if (fSqrDistance < 0.0)
                {
                    fSqrDistance = 0.0;
                }

                //  m_kClosestPoint0 = m_rkVector;
                return (v0 + ((float)fS) * kEdge0 + ((float)fT) * kEdge1);
            }
        }
    
        /// <summary>
        /// Returns true if ptTest lies within the triangle defined by pt1, pt2, pt3
        /// Code taken from http://www.blackpawn.com/texts/pointinpoly/default.html
        /// </summary>
        /// <param name="f"></param>
        /// <param name="pt"></param>
        /// <returns></returns>
        private bool pointInTriangle(Vector3 pt1, Vector3 pt2, Vector3 pt3, Vector3 ptTest)
        {
            // Compute vectors        
            Vector3 v0 = pt3 - pt1;
            Vector3 v1 = pt2 - pt1;
            Vector3 v2 = ptTest - pt1;

            // Compute dot products
            float dot00 = Vector3.Dot(v0, v0);
            float dot01 = Vector3.Dot(v0, v1);
            float dot02 = Vector3.Dot(v0, v2);
            float dot11 = Vector3.Dot(v1, v1);
            float dot12 = Vector3.Dot(v1, v2);

            // Compute barycentric coordinates
            float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            if (u + v <= 1 && u > 0 && v > 0)
                return true;
            return false;
        }
        /// <summary>
        /// Triangle Ray intersection test
        /// Code taken from http://www.ziggyware.com/readarticle.php?article_id=78
        /// </summary>
        /// <param name="ray_origin"></param>
        /// <param name="ray_direction"></param>
        /// <param name="vert0"></param>
        /// <param name="vert1"></param>
        /// <param name="vert2"></param>
        /// <param name="t"></param>
        /// <param name="u"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        private bool intersectTriangleRay(Vector3 ray_origin, Vector3 ray_direction,
                    Vector3 vert0, Vector3 vert1, Vector3 vert2,
                    out float t, out float u, out float v)
        {
            t = 0; u = 0; v = 0;

            Vector3 edge1 = vert1 - vert0;
            Vector3 edge2 = vert2 - vert0;

            Vector3 tvec, pvec, qvec;
            float det, inv_det;

            pvec = Vector3.Cross(ray_direction, edge2);

            det = Vector3.Dot(edge1, pvec);

            if (det > -0.00001f)
                return false;

            inv_det = 1.0f / det;

            tvec = ray_origin - vert0;

            u = Vector3.Dot(tvec, pvec) * inv_det;
            if (u < -0.001f || u > 1.001f)
                return false;

            qvec = Vector3.Cross(tvec, edge1);

            v = Vector3.Dot(ray_direction, qvec) * inv_det;
            if (v < -0.001f || u + v > 1.001f)
                return false;

            t = Vector3.Dot(edge2, qvec) * inv_det;

            if (t <= 0)
                return false;

            return true;
        }
    }

}
