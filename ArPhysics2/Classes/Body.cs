using BulletSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ArPhysics2
{
    public class Body
    {
        const float BOX_MASS_SHAPE_FACTOR = 1;
        const float SPHERE_MASS_SHAPE_FACTOR = 2f * (4f / 3f * MathHelper.Pi) / 2f / 2f / 2f; // diameter is used

        public bool DrawEnabled { get; set; } = true;

        private bool physicsEnabled = false;
        public bool PhysicsEnabled
        {
            set
            {
                if (!physicsEnabled && value == true)
                    world.AddRigidBody(rigidBody);
                else if (physicsEnabled && value == false)
                    world.RemoveRigidBody(rigidBody);
                physicsEnabled = value;
            }

            get => physicsEnabled;
        }
        public Matrix Pose
        {
            set
            {
                rigidBody.MotionState.WorldTransform = value;
                rigidBody.WorldTransform = value;
            }
            get => rigidBody.MotionState.WorldTransform;
        }

        //set all these fields before any draw call
        private Model model;
        private DynamicsWorld world;
        public Matrix View { set; get; } = Matrix.Identity;
        public Matrix Projection { set; get; }


        public BasicEffect ColorEffect { set; get; }
        public Effect DepthEffect { set; get; }


        Vector3 scale = Vector3.One;
        public Vector3 Scale
        {
            set
            {
                scale = value;
                meshScaleMatrix = Matrix.CreateScale(value);
                rigidBody.CollisionShape.LocalScaling = value;
                Density = densityKgOverM3; // update mass
            }

            get => scale;
        }


        // zero density makes the body kinematic
        float densityKgOverM3;
        public float Density
        {
            set
            {
                densityKgOverM3 = value;
                float mass = value * scale.X * scale.Y * scale.Z * massShapeFactor;
                rigidBody.SetMassProps(mass, rigidBody.CollisionShape.CalculateLocalInertia(mass));
                rigidBody.Activate(); // if previously kinematic, it could be sleeping and could float in the air.
            }

            get => densityKgOverM3;
        }

        public Vector3 Gravity
        {
            get => rigidBody.Gravity;
            set => rigidBody.Gravity = value;
        }


        RigidBody rigidBody;
        Matrix meshScaleMatrix = Matrix.Identity;
        float massShapeFactor;

        public bool HasRigidBody(RigidBody rb) => rigidBody == rb;

        // Put 0 mass to create a kinematic body.
        public Body(DynamicsWorld world, Model model, Shape shape, Matrix initPose, float densityKgOverM3)
        {
            this.world = world;
            CollisionShape cShape;
            switch (shape)
            {
                case Shape.BOX:
                    cShape = new BoxShape(0.5f);
                    massShapeFactor = BOX_MASS_SHAPE_FACTOR;
                    break;
                case Shape.SPHERE:
                    cShape = new SphereShape(0.5f);
                    massShapeFactor = SPHERE_MASS_SHAPE_FACTOR;
                    break;
                default:
                    cShape = new BoxShape(0.5f);
                    massShapeFactor = BOX_MASS_SHAPE_FACTOR;
                    break;
            }

            //"using" calls .Dispose() automatically
            using (var info = new RigidBodyConstructionInfo(1, new DefaultMotionState(initPose), cShape))
                rigidBody = new RigidBody(info);
            PhysicsEnabled = true;

            this.model = model;
            Density = densityKgOverM3;
            Scale = new Vector3(1);
        }

        public void Draw(bool transparent = false)
        {
            if (DrawEnabled && !(!transparent ^ ColorEffect.Alpha == 1)) // draw only if effect alpha is congruent with flag
            {
                //since the same model and effect are used by multiple bodies, I must reassign the data each draw cycle
                foreach (var mesh in model.Meshes)
                {
                    foreach (var meshPart in mesh.MeshParts)
                    {
                        ColorEffect.World = meshScaleMatrix * rigidBody.MotionState.WorldTransform;
                        ColorEffect.View = View;
                        ColorEffect.Projection = Projection;
                        meshPart.Effect = ColorEffect;
                    }
                    mesh.Draw();
                }
            }
        }

        public void DrawDepth()
        {
            foreach (var mesh in model.Meshes)
            {
                DepthEffect.Parameters["WVP"].SetValue(meshScaleMatrix * rigidBody.MotionState.WorldTransform * View * Projection);
                DepthEffect.Techniques[0].Passes[0].Apply();
                foreach (var meshpart in mesh.MeshParts)
                    meshpart.Effect = DepthEffect;
                mesh.Draw();
            }
        }

        bool disposed = false;
        public void Dispose()
        {
            if (!disposed && rigidBody != null)
            {
                PhysicsEnabled = false; // <- removes rigid body if necessary
                rigidBody.MotionState?.Dispose();
                rigidBody.CollisionShape.Dispose();
                rigidBody.Dispose();

                disposed = true;
            }
        }
    }
}
