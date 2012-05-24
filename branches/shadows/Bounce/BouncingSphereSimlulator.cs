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
// Copyright 2012. All rights reserved.                                                       //
// ========================================================================================== //


using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using Microsoft.Xna.Framework;

using CollidableModel;

namespace Bounce
{
    /// <summary>
    /// Properties for one simulated sphere
    /// </summary>
    class SphereProperties
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 angularMomentum;
        public Quaternion rotation;

        //note: could be omitted if we only simulate spheres (inertia tensors are diagonal matrices)
        public Matrix invBodyInertia;
        public Matrix invWorldInertia;
    }


    /// <summary>
    /// Rigid body physics simulator. Uses simple euler integration to solve the differential
    /// equations at each time step. Collisions are resolved in an impulse driven way. The simulated 
    /// spheres are kept in linked lists for fast addition and removal, getSpheres() allows the 
    /// retrieval of the world matrices for all spheres. For increased accuracy, the simulator
    /// uses substepping (each time step is subdivided into a number of substeps).
    /// </summary>
    public class BouncingSphereSimlulator
    {
        private LinkedList<SphereProperties> mSimulatedSpheres;
        private LinkedList<SphereProperties> mSimulatedSpheresNextStep;

        /// <summary>
        /// Returns the number of currently simulated spheres
        /// </summary>
        public int numSpheres
        {
            get { return mSimulatedSpheres.Count; }
        }

        /// <summary>
        /// Returns the colliding faces, along with the number of collisions for each face
        /// </summary>
        private Dictionary<Face, uint> mCollidingFaces;
        public Dictionary<Face, uint> collidingFaces
        {
            get { return mCollidingFaces; }
        }
        

        // sphere physics properties: same for all spheres
        private float mMass;         
        private float mLinearFriction;
        private float mAngularDamping;

        // Radius of simulated spheres
        private float mSphereRadius;
        public float sphereRadius
        {
            get { return mSphereRadius; }
            set { mSphereRadius = value; }
        }

        /// <summary>
        /// Gravity force
        /// </summary>
        private Vector3 mGravity;
        public Vector3 gravity
        {
            get { return mGravity; }
            set { mGravity = value; }
        }

        float timeScale = 0.005f;

        // Number of substeps performed for each time step
        private int mNumSubSteps;
        public int numSubSteps
        {
            get { return mNumSubSteps; }
            set { mNumSubSteps = value; }
        }

        /// <summary>
        /// Total time needed for calculating the collisions of during the last update()
        /// </summary>
        private Stopwatch mTimeCollisions;
        public double timeCollisions
        {
            get { return convertToMiliseconds( mTimeCollisions.ElapsedTicks ); }        
        }

        /// <summary>
        /// Total time needed for by physics simulation during the last update() (collision detection excluded)
        /// </summary>
        private Stopwatch mTimePhysics;
        public double timePhysics
        {

            get { return convertToMiliseconds(mTimePhysics.ElapsedTicks); }
        }

        /// <summary>
        /// Number of times the bsp tree was queried for collisions during last update()
        /// </summary>
        private int mNumCollisionTests;
        public int numCollisionTests
        {
            get { return mNumCollisionTests; }
        }

        /// <summary>
        /// Conversion routine for converting stopwatch ticks into miliseconds
        /// </summary>
        /// <param name="numTicks"></param>
        /// <returns></returns>
        private double convertToMiliseconds(long numTicks)
        {
            double result = 0.0;

            // add integral part first
            result += numTicks / Stopwatch.Frequency;

            // get remaining fraction and convert to double
            long remainingTicks = numTicks % Stopwatch.Frequency;
            result += ( (double)(remainingTicks) / (double)(Stopwatch.Frequency) );

            // convert to ms
            return 1000 * result;
        }

        BspTree mBspTree;

        public BouncingSphereSimlulator(BspTree bspTree)
        {
            // 
            mMass = 1.0f;
            mLinearFriction = 0.15f;
            mAngularDamping = 0.995f;

            mSphereRadius = 1.35f;
            mGravity = new Vector3(0.0f, -9.81f, 0.0f);

            mBspTree = bspTree;

            // for simulating the spheres two linked lists are used: 
            // new sphere properties are written one by one to mSimulatedSpheresNextStep,
            // and at the end of the simulation loop all new properties are copied to
            // mSimulatedSpheres at once.
            mSimulatedSpheres = new LinkedList<SphereProperties>();
            mSimulatedSpheresNextStep = new LinkedList<SphereProperties>();
            mCollidingFaces = new Dictionary<Face, uint>();

            mNumSubSteps = 3;

            // init performance measurement timers
            mTimeCollisions = new Stopwatch();
            mTimePhysics = new Stopwatch();


        }

        /// <summary>
        /// Adds a new sphere to the simulator
        /// </summary>
        /// <param name="position">Location of the new sphere</param>
        /// <param name="velocity">Velocity vector of the new sphere</param>
        public void addSphere(Vector3 position, Vector3 velocity)
        {
            SphereProperties newSphere = new SphereProperties();
            newSphere.position = position;
            newSphere.velocity = velocity;
            newSphere.angularMomentum = Vector3.Zero;
            newSphere.rotation = Quaternion.Identity;
            newSphere.invBodyInertia = Matrix.Identity;
            newSphere.invWorldInertia = Matrix.Identity;

            mSimulatedSpheres.AddLast(newSphere);
            mSimulatedSpheresNextStep.AddLast(newSphere);

        }

        /// <summary>
        /// Advance the simulator by one timestep
        /// </summary>
        /// <param name="time"></param>
        public void update(GameTime time)
        {
            // restest counters and stop watches
            mNumCollisionTests = 0;
            mTimePhysics.Reset();
            mTimeCollisions.Reset();

            mTimePhysics.Start();
            mCollidingFaces.Clear();

            // time delta
            float dt = time.ElapsedGameTime.Milliseconds * timeScale / numSubSteps;

            // for each substep
            for (int step = 0; step < numSubSteps; step++)
            {
                // iterate through all spheres
                LinkedListNode<SphereProperties> curSimulatedSphere = mSimulatedSpheres.First;
                LinkedListNode<SphereProperties> nextSimulatedSphere = mSimulatedSpheresNextStep.First;

                while (curSimulatedSphere != null)
                {
                    // copy current sphere properties
                    SphereProperties sphere = curSimulatedSphere.Value;

                    // compute forces
                    Vector3 forces = -mLinearFriction * curSimulatedSphere.Value.velocity;
                    forces += mGravity;

                    // compute acceleration
                    Vector3 acceleration = forces / mMass;

                    // integrate (keep it simple: just use euler)
                    sphere.velocity += acceleration * dt;
                    sphere.position += sphere.velocity * dt;

                    // compute new angular momentum (damping)
                    sphere.angularMomentum *= mAngularDamping;

                    // integrate angular momentum
                    Matrix rot = Matrix.CreateFromQuaternion(curSimulatedSphere.Value.rotation);
                    sphere.invWorldInertia = rot * sphere.invBodyInertia * Matrix.Transpose(rot);

                    // compute angular velocity
                    Vector3 omega = Vector3.Transform(sphere.angularMomentum, sphere.invWorldInertia);

                    // integrate rotational part into quaternion
                    Quaternion quatRotDot = new Quaternion(omega, 0.0f) * sphere.rotation;
                    quatRotDot *= -0.5f;
                    quatRotDot *= dt;

                    quatRotDot += sphere.rotation;
                    quatRotDot.Normalize();

                    sphere.rotation = quatRotDot;


                    // check for arena collisions and resolve them
                    handleArenaCollisions(ref sphere);

                    // copy new sphere properties to updated buffer
                    nextSimulatedSphere.Value = sphere;

                    // remove spheres below arena
                    LinkedListNode<SphereProperties> oldNode = curSimulatedSphere;
                    LinkedListNode<SphereProperties> oldNode2 = nextSimulatedSphere;

                    curSimulatedSphere = curSimulatedSphere.Next;
                    nextSimulatedSphere = nextSimulatedSphere.Next;

                    if (sphere.position.Y < -200)
                    {
                        mSimulatedSpheres.Remove(oldNode);
                        mSimulatedSpheresNextStep.Remove(oldNode2);
                    }

                    
                }

                // swap lists (update all spheres at once)
                LinkedList<SphereProperties> tmp = mSimulatedSpheres;
                mSimulatedSpheres = mSimulatedSpheresNextStep;
                mSimulatedSpheresNextStep = tmp;

                // compute and resolve sphere-sphere collisions
               handleSphereSphereCollisions();

            }

            // stop stopwatch
            mTimePhysics.Stop();

        }

        private void handleArenaCollisions(ref SphereProperties sphere)//(GameTime gameTime)
        {

            // compute collisions with level and store them in mCollisionPoints
            mTimePhysics.Stop();
            mTimeCollisions.Start();

            LinkedList<Face> arenaCollidingFaces = new LinkedList<CollidableModel.Face>();
            LinkedList<Vector3> arenaCollisionPoints = new LinkedList<Vector3>();

            mBspTree.collisions(new BoundingSphere(sphere.position, mSphereRadius), arenaCollidingFaces, arenaCollisionPoints);

             mNumCollisionTests++;
            mTimeCollisions.Stop();
            mTimePhysics.Start();

            // add collisions to colliding faces
            foreach (Face f in arenaCollidingFaces)
            {
      
                if (mCollidingFaces.ContainsKey(f))
                    mCollidingFaces[f]++;
                else
                    mCollidingFaces.Add(f, 1);
         
            }

            // are there any collisions?
            if (arenaCollisionPoints.Count != 0)
            {
                // compute average of all collision points
                Vector3 avgColPoint = Vector3.Zero;
                Vector3 avgFaceNormal = Vector3.Zero;
                Vector3 closestNormal = Vector3.Zero;
                float closestDistance = float.MaxValue;

                LinkedListNode<Vector3> curPoint = arenaCollisionPoints.First;
                LinkedListNode<Face> curFace = arenaCollidingFaces.First;
                while (curPoint != null)
                {
                    avgFaceNormal += curFace.Value.faceNormal;
                    avgColPoint += curPoint.Value;

                    // update closest normal
                    float d = (sphere.position - curPoint.Value).LengthSquared();
                    if (d < closestDistance)
                    {
                        closestDistance = d;
                        closestNormal = curFace.Value.faceNormal;
                    }
                    curPoint = curPoint.Next;
                    curFace = curFace.Next;
                }

                // this can happen at very thin walls for example
                if (avgFaceNormal == Vector3.Zero)
                    avgFaceNormal = closestNormal;

                // normalize
                avgFaceNormal.Normalize();
                avgColPoint /= arenaCollisionPoints.Count;

                // --- resolve collision ------
                resolveArenaCollisions(ref sphere, avgColPoint, avgFaceNormal);
            }
        }



        /// <summary>
        /// resolves player-level collisions
        /// </summary>
        private void resolveArenaCollisions(ref SphereProperties sphere, Vector3 avgColPoint, Vector3 avgFaceNormal)
        {
            // average inverse collision direction
            Vector3 avgColVec = sphere.position - avgColPoint;

            // collision direction
            Vector3 r = Vector3.Normalize(-avgColVec);
            r *= mSphereRadius;

            // move sphere out of colliding state
            sphere.position += (-r - avgColVec);

            // the coefficient of restitution (1 for perfect elastic pulse)
            float e = 0.9f;

            // the relative velocity
            float relativeVelocity = Vector3.Dot(sphere.velocity, avgFaceNormal);

            // intermediate computations
            Vector3 tmp = Vector3.Cross(r, avgFaceNormal);
            tmp = Vector3.Transform(tmp, sphere.invWorldInertia);
            tmp = Vector3.Cross(tmp, r);

            // the impulse's length
            float j = (-(1 + e) * relativeVelocity) / ((1 / mMass) + Vector3.Dot(avgFaceNormal, tmp));

            // the actual impulse
            Vector3 J = avgFaceNormal * j;

            // apply the impulse to the linear velocity
            sphere.velocity += (J / mMass);

            // the impulse torque
            Vector3 torqueImpuls = Vector3.Cross(r, J);

            // apply the torque to the angular velocity
            sphere.angularMomentum += torqueImpuls;

            // initiate rotation by changing angular momentum based on velocity of particle in contact with arena
            Vector3 omega = Vector3.Transform(sphere.angularMomentum, sphere.invBodyInertia);
            Vector3 collisionPointVelocity = Vector3.Cross(omega, r);

            // velocity difference of negative particle velocity and center of mass velocity
            Vector3 velocityDifference = collisionPointVelocity + sphere.velocity;

            // adjust angular momentum such that the velocity of the colliding particle equals the velocity of the center of mass
            sphere.angularMomentum = -Vector3.Cross(r, collisionPointVelocity - velocityDifference);
        }

        private void handleSphereSphereCollisions()
        {
            // now handle sphere-sphere collisions
            LinkedListNode<SphereProperties> curSimulatedSphere = mSimulatedSpheres.First;
            LinkedListNode<SphereProperties> nextSimulatedSphere = mSimulatedSpheresNextStep.First;

            float radiusSquared = mSphereRadius * mSphereRadius;

            // for each (unordered) sphere-sphere pair: 
            while (curSimulatedSphere != null)
            {
                SphereProperties curSphere = curSimulatedSphere.Value;

                LinkedListNode<SphereProperties> otherSphere = curSimulatedSphere.Next;
                while (otherSphere != null)
                {
                    // spheres collide? call resolveSphereSphereCollision()
                    if (Vector3.Distance(curSphere.position, otherSphere.Value.position) <= 2*mSphereRadius )
                    {
                        SphereProperties other = otherSphere.Value;
                        resolveSphereSphereCollision(ref curSphere, ref other);

                        // update the other sphere
                        otherSphere.Value = other;
                    }

                    otherSphere = otherSphere.Next;

                }

                // update current sphere
                nextSimulatedSphere.Value = curSphere;

                // advance to next sphere
                curSimulatedSphere = curSimulatedSphere.Next;
                nextSimulatedSphere = nextSimulatedSphere.Next;
            }

            // swap lists (update all spheres at once)
            LinkedList<SphereProperties> tmp = mSimulatedSpheres;
            mSimulatedSpheres = mSimulatedSpheresNextStep;
            mSimulatedSpheresNextStep = tmp;
        }

        private void resolveSphereSphereCollision(ref SphereProperties cur, ref SphereProperties other)
        {
            // the coefficient of restitution (1 for perfect elastic pulse)
            float e = 0.5f;
            float j = 0.0f;

            // compute penetration
            float penetration = 2*mSphereRadius - Vector3.Distance( cur.position, other.position );

            // resolve player collision based on their impulses
            Vector3 rOne = Vector3.Normalize(other.position - cur.position);
            Vector3 rTwo = -rOne;
            Vector3 colNormal = rOne;

            rOne *= mSphereRadius;
            rTwo *= mSphereRadius;

            // project positions out of collisions
            cur.position += rTwo * penetration / 2.0f;
            other.position += rOne * penetration / 2.0f;

            // relativ velocity
            float relVelocity = Vector3.Dot(colNormal, cur.velocity - other.velocity);

            // intermediate computations
            Vector3 tmpOne = Vector3.Cross(rOne, colNormal);
            tmpOne = Vector3.Transform(tmpOne, cur.invBodyInertia);
            tmpOne = Vector3.Cross(tmpOne, rOne);

            Vector3 tmpTwo = Vector3.Cross(rTwo, colNormal);
            tmpTwo = Vector3.Transform(tmpTwo, other.invBodyInertia);
            tmpTwo = Vector3.Cross(tmpTwo, rTwo);

            // compute j
            j = (-(1.0f + e) * relVelocity) / ((1.0f / mMass) + (1.0f / mMass) + Vector3.Dot(colNormal, tmpOne) + Vector3.Dot(colNormal, tmpTwo));

            // compute J
            Vector3 J = j * colNormal;

            // apply the impulse to the linear velocities
            cur.velocity += J / mMass;
            other.velocity -= J / mMass;

            // compute impulse torque
            Vector3 torqueImpulsTwo = Vector3.Cross(rTwo, -J);
            Vector3 torqueImpulsOne = Vector3.Cross(rOne, J);

            // apply the torque to the angular momentum
            cur.angularMomentum += torqueImpulsOne;
            other.angularMomentum -= torqueImpulsTwo;
        }


        /// <summary>
        /// Converts the sphere properties for each sphere to a world matrix suitable for 
        /// rendering the sphere.
        /// </summary>
        /// <param name="result">Matrices are stored here</param>
        /// <param name="num">The number of matrices is stored here</param>
        public void getSpheres(ref Matrix[] result, ref int num)
        {
            num = mSimulatedSpheres.Count;

            LinkedListNode<SphereProperties> curSphere = mSimulatedSpheres.First;
            uint i = 0;
            while (curSphere != null)
            {
                result[i] = Matrix.CreateFromQuaternion(curSphere.Value.rotation) *
                            Matrix.CreateScale(mSphereRadius) * 
                            Matrix.CreateTranslation(curSphere.Value.position);
                            

                curSphere = curSphere.Next;
                i++;
            }
        }
    }
}
