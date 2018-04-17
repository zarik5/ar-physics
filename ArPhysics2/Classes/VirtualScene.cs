using BulletSharp;
using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using KBody = Microsoft.Kinect.Body;

namespace ArPhysics2
{
    class VirtualScene
    {
        //constants
        public const float DEFAULT_DENSITY_KG_OVER_M3 = 500;
        public const float STROKE_THICKNESS_M = .02f;
        public const float MAX_LENGTH_STROKE = .05f;
        const float DEFAULT_PLANE_HEIGHT_M = 1;
        const float DEFAULT_MARGIN_DIST_M = 1.5f;
        const float NO_MARGIN_DIST_M = 20;
        const float DROP_SPAWN_MARGIN_DIST_M = .5f;
        const float TABLE_FLOOR_THRESH_M = .6f;
        const float DROP_SPAWN_Y_M = 1f;
        const float FLOOR_DROP_MIN_SCALE = .20f;
        const float FLOOR_DROP_MAX_SCALE = .70f;
        const float TABLE_DROP_MIN_SCALE = .10f;
        const float TABLE_DROP_MAX_SCALE = .40f;
        const float DEPTH_SPHERES_SIZE_MUL = 1.7f;
        const int MAX_NUM_VIRTUAL_BODIES = 50;
        public static readonly Vector3 DEFAULT_GRAVITY = new Vector3(0, y: -10, 0);
        static readonly Matrix DEFAULT_POSE = Matrix.CreateTranslation(new Vector3(0, 0, z: 10)); // 10m behind camera
        static readonly TimeSpan STROKES_EXPIRATION_SPAN = new TimeSpan(0, 0, 0, seconds: 10);
        static readonly TimeSpan SANDBOX_IDLE_SPAN_FOR_DROP = new TimeSpan(0, 0, 0, seconds: 20);
        static readonly TimeSpan SANDBOX_DROP_SPAN = new TimeSpan(0, 0, 0, seconds: 5);
        static readonly TimeSpan PHYSICS_DROP_SPAN = new TimeSpan(0, 0, 0, seconds: 3);
        
        const float SPECIAL_OBJECT_PROB = 0.15f;
        static readonly Shape[] NORMAL_SHAPES = new[] { Shape.CUBE, Shape.SPHERE };
        static readonly Color[] NORMAL_COLORS = new[]
        {
            Color.RED, Color.ORANGE, Color.YELLOW, Color.GREEN, Color.BLUE, Color.PURPLE, Color.PINK, Color.GRAY
        };
        static readonly Color[] STROKE_COLORS = new[]
        {
            Color.RED, Color.ORANGE, Color.YELLOW, Color.GREEN, Color.BLUE, Color.PURPLE, Color.PINK,
        };
        static readonly (Shape, Color, float density, float gravity)[] SPECIAL_OBJECTS = new[]
        {
            (Shape.SPHERE, Color.BLACK, 10000f, -20f),
            (Shape.SPHERE, Color.TRANSPARENT_LIGHT_BLUE, 20f, 1f),
            (Shape.RANDOM, Color.RANDOM, 20f, 2f),
        };

        // public properties:

        Matrix view = Matrix.Identity;
        public Matrix View
        {
            set
            {
                view = value;
                virtualBodies.ToList().ForEach(body => body.View = value);
                depthSpheres.ForEach(sphere => sphere.View = value);
                usersBodies.ToList().ForEach(pair => pair.Value.View = value);
                strokeBodies.ToList().ForEach(pair => pair.body.View = value);
                boxPlane.View = value;
            }
        }

        Matrix projection = Matrix.Identity;
        public Matrix Projection
        {
            set
            {
                projection = value;
                virtualBodies.ToList().ForEach(body => body.Projection = value);
                depthSpheres.ForEach(sphere => sphere.Projection = value);
                usersBodies.ToList().ForEach(pair => pair.Value.Projection = value);
                strokeBodies.ToList().ForEach(pair => pair.body.Projection = value);
                boxPlane.Projection = value;
            }
        }

        // bodies
        Queue<Body> virtualBodies = new Queue<Body>();
        List<Body> depthSpheres = new List<Body>();
        Body boxPlane;
        Dictionary<ulong, SkeletonBody> usersBodies = new Dictionary<ulong, SkeletonBody>();
        Queue<(Body body, DateTime)> strokeBodies = new Queue<(Body, DateTime)>();
        
        //physics
        CollisionConfiguration collisionConf;
        CollisionDispatcher collisionDispatcher;
        BroadphaseInterface broadphase;
        DynamicsWorld world;
        
        // from graphics
        IReadOnlyDictionary<Color, BasicEffect> normalColorEffects;
        IReadOnlyDictionary<Color, BasicEffect> flatColorEffects;
        BasicEffect grayEffect;
        Effect depthEffect;
        IReadOnlyDictionary<Shape, Model> models;

        // misc
        GameMode gameMode;
        int numDepthSpheres;
        float depthSpheresSpacingAt1M;
        float planeHeightM;
        DateTime lastInteractionTime = DateTime.Now;
        DateTime lastDropTime = DateTime.Now;
        Matrix dropSpawnPose = Matrix.Identity;
        Random rand = new Random();
        
        // utility methods:

        float UniformRnd(float min, float max) => (float)new Random().NextDouble() * (max - min) + min;

        Body CreateShape(Shape shape, BasicEffect colorEffect = null,
            float densityKgOverM3 = DEFAULT_DENSITY_KG_OVER_M3, Matrix? pose = null)
        {
            return new Body(world, models[shape], shape, pose ?? DEFAULT_POSE, densityKgOverM3)
            {
                ColorEffect = colorEffect ?? normalColorEffects[NORMAL_COLORS.Rnd()],
                DepthEffect = depthEffect,
                View = view,
                Projection = projection,
            };
        }

        public void Initialize(Effect depthEffect, int numDepthSpheres, float depthSpheresSpacingAt1M,
            IReadOnlyDictionary<Shape, Model> models,
            IReadOnlyDictionary<Color, BasicEffect> normalColorEffects,
            IReadOnlyDictionary<Color, BasicEffect> flatColorEffects)
        {
            //create physics world
            collisionConf = new DefaultCollisionConfiguration();
            collisionDispatcher = new CollisionDispatcher(collisionConf);
            broadphase = new DbvtBroadphase();
            broadphase.OverlappingPairCache.SetInternalGhostPairCallback(new GhostPairCallback()); // for grabbing functionality
            world = new DiscreteDynamicsWorld(collisionDispatcher, broadphase, null, collisionConf)
            {
                Gravity = DEFAULT_GRAVITY,
            };

            this.depthEffect = depthEffect;
            this.normalColorEffects = normalColorEffects;
            this.flatColorEffects = flatColorEffects;
            this.models = models;
            grayEffect = normalColorEffects[Color.GRAY];


            this.numDepthSpheres = numDepthSpheres;
            this.depthSpheresSpacingAt1M = depthSpheresSpacingAt1M;
            for (int i = 0; i < numDepthSpheres; i++) // spread the spheres behind the camera to avoid a massive amount of collisions that causes lag
                depthSpheres.Add(CreateShape(Shape.SPHERE, grayEffect, 0,
                    Matrix.CreateTranslation(new Vector3(0, 0, 2 * i + 10))));

            boxPlane = CreateShape(Shape.BOX, grayEffect, 0);
            UpdatePlane(.7f, 2); // these values will be overwritten when the kinect finds the plane
        }

        public void Reset()
        {
            //users must be dispose first because they could access the rigidbody of some objects
            usersBodies.ToList().ForEach(pair => pair.Value.Dispose());
            usersBodies.Clear();

            virtualBodies.ToList().ForEach(body => body.Dispose());
            virtualBodies.Clear();

            strokeBodies.ToList().ForEach(pair => pair.body.Dispose());
            strokeBodies.Clear();

            lastDropTime = DateTime.Now;
            lastInteractionTime = DateTime.Now;

            // do not reset plane, calibration is not immediate and last calibration is probably good
            // after a game mode change calibration is requested anyway.
        }

        public void UpdatePlane(float heightM, float marginDistM)
        {
            planeHeightM = heightM;
            if (heightM == 0 && marginDistM == 0)
            {
                heightM = DEFAULT_PLANE_HEIGHT_M;
                marginDistM = DEFAULT_MARGIN_DIST_M;
            }
            if (heightM > TABLE_FLOOR_THRESH_M)
                marginDistM = NO_MARGIN_DIST_M;

            boxPlane.Scale = new Vector3(marginDistM * 2);
            boxPlane.Pose = Matrix.CreateTranslation(new Vector3(0, -marginDistM - heightM, 0));


            dropSpawnPose = Matrix.CreateTranslation(new Vector3(0, DROP_SPAWN_Y_M,
                -Math.Min(marginDistM - DROP_SPAWN_MARGIN_DIST_M, 2.5f)));

        }

        public void SetGameMode(GameMode mode)
        {
            gameMode = mode;
            Reset();
        }

        public void Update(GameTime deltaTime, Vector3[] depthSpheresCenters, IReadOnlyDictionary<ulong, KBody> bodies)
        {
            var invView = Matrix.Invert(view);

            if (virtualBodies.Count > MAX_NUM_VIRTUAL_BODIES)
                virtualBodies.Dequeue().Dispose();
            // a cycle is not mandatory, excess bodies can be purged on successive update steps
            if (strokeBodies.Count > 0)
            {
                var (strokeBody, time) = strokeBodies.Peek();
                if (DateTime.Now - time > STROKES_EXPIRATION_SPAN)
                    strokeBodies.Dequeue().body.Dispose();
            }

            // update depth spheres
            for (int i = 0; i < numDepthSpheres; i++)
            {
                var sphere = depthSpheres[i];
                var center = depthSpheresCenters[i];

                if (!float.IsNegativeInfinity(center.Y))
                {
                    var pose = Matrix.CreateTranslation(center) * invView;
                    if (pose.Translation.Y > -planeHeightM + .30f)
                    {
                        sphere.DrawEnabled = true;
                        sphere.PhysicsEnabled = true;

                        sphere.Pose = pose;
                        sphere.Scale = new Vector3(-center.Z * depthSpheresSpacingAt1M * DEPTH_SPHERES_SIZE_MUL);
                    }
                    else
                    {
                        sphere.DrawEnabled = false;
                        sphere.PhysicsEnabled = false;
                    }
                }
                else
                {
                    sphere.DrawEnabled = false;
                    sphere.PhysicsEnabled = false;
                }
            }

            //update user bodies
            foreach (var (id, user) in bodies)
            {
                if (usersBodies.ContainsKey(id) == false)
                {
                    usersBodies.Add(id,
                        new SkeletonBody(world, depthEffect, models, normalColorEffects,
                            strokeBody => strokeBodies.Enqueue((strokeBody, DateTime.Now)))
                        {
                            View = view,
                            Projection = projection
                        });
                }
                usersBodies[id].Update(user);
            }
            
            // remove bodies of users that went out of frame
            var targetIds = bodies.Keys;
            for (int i = usersBodies.Count - 1; i >= 0; i--)
            {
                var id = usersBodies.ElementAt(i).Key;
                if (targetIds.Contains(id) == false)
                {
                    usersBodies.ElementAt(i).Value.Dispose();
                    usersBodies.Remove(id);
                }
            }

            // drop shape
            var now = DateTime.Now;
            if ((gameMode == GameMode.SANDBOX && now - lastInteractionTime > SANDBOX_IDLE_SPAN_FOR_DROP
                    && now - lastDropTime > SANDBOX_DROP_SPAN)
                || (gameMode == GameMode.HIW_PHYSICS && now - lastDropTime > PHYSICS_DROP_SPAN))
            {
                lastDropTime = now;

                // spawn random body
                Body newBody = CreateShape(NORMAL_SHAPES.Rnd());

                if (planeHeightM < TABLE_FLOOR_THRESH_M)
                {

                }
                newBody.Scale = new Vector3(planeHeightM < TABLE_FLOOR_THRESH_M
                    ? UniformRnd(TABLE_DROP_MIN_SCALE, TABLE_DROP_MAX_SCALE)
                    : UniformRnd(FLOOR_DROP_MIN_SCALE, FLOOR_DROP_MAX_SCALE));
                newBody.Pose = dropSpawnPose;

                virtualBodies.Enqueue(newBody);
            }

            world.StepSimulation((float)deltaTime.ElapsedGameTime.TotalSeconds);
        }

        public void Draw(SceneDrawMode drawMode)
        {
            if (drawMode == SceneDrawMode.VIRTUAL_DEPTH)
            {
                foreach (var body in virtualBodies)
                    body.DrawDepth();
                foreach (var (stroke, _) in strokeBodies)
                    stroke.DrawDepth();
                foreach (var (_, user) in usersBodies)
                    user.DrawDepth();
            }
            else
            {
                bool transpFlag = drawMode == SceneDrawMode.SANDBOX_TRANSPARENT
                    || drawMode == SceneDrawMode.PHYSICS_TRANSPARENT;

                bool drawingPhysics = drawMode == SceneDrawMode.PHYSICS_OPAQUE
                    || drawMode == SceneDrawMode.PHYSICS_TRANSPARENT;

                if (drawingPhysics)
                {
                    foreach (var body in depthSpheres)
                        body.Draw(transpFlag);
                    boxPlane.Draw(transpFlag);
                }
                foreach (var body in virtualBodies)
                    body.Draw(transpFlag);
                foreach (var (stroke, _) in strokeBodies)
                    stroke.Draw(transpFlag);
                foreach (var (_, user) in usersBodies)
                    user.Draw(drawingPhysics, transpFlag);
            }
        }

        // interactions:

        public void BeginCreatingObject(ulong userId, Shape shape = Shape.RANDOM, Color color = Color.RANDOM)
        {
            if (usersBodies.ContainsKey(userId) == false)
                return;

            var density = DEFAULT_DENSITY_KG_OVER_M3;
            var gravity = DEFAULT_GRAVITY.Y;

            if (shape == Shape.RANDOM && color == Color.RANDOM && rand.NextDouble() < SPECIAL_OBJECT_PROB)
                (shape, color, density, gravity) = SPECIAL_OBJECTS.Rnd();
            if (shape == Shape.RANDOM)
                shape = NORMAL_SHAPES.Rnd();
            if (color == Color.RANDOM)
                color = NORMAL_COLORS.Rnd();

            var creatingObject = CreateShape(shape, normalColorEffects[color]);
            creatingObject.Density = density;
            creatingObject.Gravity = new Vector3(0, gravity, 0);
            usersBodies[userId].BeginCreatingObject(creatingObject, shape == Shape.BOX);

            lastInteractionTime = DateTime.Now;
        }

        public void FinishCreatingObject(ulong userId)
        {
            if (usersBodies.ContainsKey(userId) == false)
                return;
            var createdObject = usersBodies[userId].FinishCreatingObject();
            if (createdObject != null)
                virtualBodies.Enqueue(createdObject);

            lastInteractionTime = DateTime.Now;
        }


        public void SetGrabState(ulong userId, JointType hand, bool enabled)
        {
            usersBodies[userId].SetGrabState(hand, enabled);

            lastInteractionTime = DateTime.Now;
        }

        public void SetDrawingState(ulong userId, JointType hand, bool enabled, Color color = Color.RANDOM)
        {
            if (color == Color.RANDOM)
                color = STROKE_COLORS.Rnd();
            usersBodies[userId].SetDrawingState(hand, enabled,
                gameMode == GameMode.HIW_PHYSICS ? normalColorEffects[color] : flatColorEffects[color]);

            lastInteractionTime = DateTime.Now;
        }


        public void Dispose()
        {
            if (world != null)
            {
                Reset();

                foreach (var sphere in depthSpheres)
                    sphere.Dispose();

                // Remove/dispose constraints
                for (int i = world.NumConstraints - 1; i >= 0; i--)
                {
                    var constraint = world.GetConstraint(i);
                    world.RemoveConstraint(constraint);
                    constraint.Dispose();
                }

                // Remove/dispose rigid bodies
                for (int i = world.NumCollisionObjects - 1; i >= 0; i--)
                {
                    var obj = world.CollisionObjectArray[i];
                    if (obj is RigidBody body && body.MotionState != null)
                    {
                        body.MotionState.Dispose();
                    }
                    world.RemoveCollisionObject(obj);
                    obj.Dispose();
                }

                world.Dispose();
            }

            broadphase?.Dispose();
            collisionDispatcher?.Dispose();
            collisionConf?.Dispose();
        }

    }
}
