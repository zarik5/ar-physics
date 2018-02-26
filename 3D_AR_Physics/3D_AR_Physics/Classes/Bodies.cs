using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StillDesign.PhysX;
using PxVector3 = StillDesign.PhysX.MathPrimitives.Vector3;
using PxMatrix = StillDesign.PhysX.MathPrimitives.Matrix;

namespace AR_Physics
{
    public class DebugEffect : BasicEffect
    {
        public DebugEffect(MainWindow main)
            : base(main.GCtrl.GraphicsDevice)
        {
            DiffuseColor = Color.LightGray.ToVector3();
            EnableDefaultLighting();
        }
    }

    public class Box : VirtColorBody
    {
        public Box(MainWindow main, PxMatrix pose, PxVector3 dimensions, float density, Color color)
            : base(main)
        {
            Model = Main.GCtrl.Dispenser.Models[0];
            MeshColor = color;
            MeshScale = dimensions;
            Initialize(new BoxShapeDescription(dimensions), new BodyDescription(density * dimensions.X * dimensions.Y * dimensions.Z), pose);
        }
    }

    public class Sphere : VirtColorBody
    {
        public Sphere(MainWindow main, PxMatrix transp, float radius, float density, Color color)
            : base(main)
        {
            Model = Main.GCtrl.Dispenser.Models[1];
            MeshColor = color;
            MeshScale = new PxVector3(radius * 2);
            Initialize(new SphereShapeDescription(radius), new BodyDescription(density * 4 / 3 * (float)(Math.PI * Math.Pow(radius, 3))), transp);
        }
    }

    //public class Football : VirtMeshBody
    //{
    //    public Football(MainWindow main, PxMatrix transp, float radius, float density)
    //        : base(main)
    //    {
    //        Model = Main.GCtrl.Dispenser.Models[1];
    //        MeshScale = new PxVector3(radius * 2);
    //        Initialize(new SphereShapeDescription(radius), new BodyDescription(density * 4 / 3 * (float)(Math.PI * Math.Pow(radius, 3))), transp);
    //    }
    //}

    public class BoxPlane : RealBody
    {
        public BoxPlane(MainWindow main, float h, float d)
            : base(main)
        {
            Model = Main.GCtrl.Dispenser.Models[0];
            MeshScale = new PxVector3(2 * -d);
            Initialize(new BoxShapeDescription(new PxVector3(2 * -d)), null, PxMatrix.Translation(new PxVector3(0, h + d, 0)));
        }
    }

    public class DepthSphere : RealBody
    {
        public DepthSphere(MainWindow main, int x, int y)
            : base(main)
        {
            Model = Main.GCtrl.Dispenser.Models[1];
            MeshScale = new PxVector3(0.04f);
            Initialize(new SphereShapeDescription(0.02f), new BodyDescription(1), PxMatrix.Translation(x, y, 0));
        }
    }


    //public class SkinnedSkeleton
    //{
    //    class BoxBone : RealBody
    //    {
    //        public BoxBone(MainWindow main, float width, PxVector3 stackPose)
    //            : base(main)
    //        {

    //        }
    //    }

    //    class CapsuleBone : RealBody
    //    {
    //        public CapsuleBone(MainWindow main, PxVector3 p1, PxVector3 p2, float thickness)
    //            : base(main)
    //        {

    //        }
    //    }

    //    class SphereJoint : RealBody
    //    {
    //        public SphereJoint(MainWindow main, PxVector3 p, float radius)
    //            : base(main)
    //        {

    //        }
    //    }

    //    //SphereJoint head;

    //    //BoxBone trunk, rArm, rForeArm, rHand,
    //    //               lArm, lForeArm, lHand,
    //    //               lThig, lLeg, lFoot,
    //    //               rThig, rLeg, rFoot;


    //    public SkinnedSkeleton(Skeleton skel)
    //    {

    //    }

    //    //0 HipCenter,
    //    //1 Spine,
    //    //2 ShoulderCenter,
    //    //3 Head,
    //    //4 ShoulderLeft,
    //    //5 ElbowLeft,
    //    //6 WristLeft,
    //    //7 HandLeft,
    //    //8 ShoulderRight,
    //    //9 ElbowRight,
    //    //10 WristRight,
    //    //11 HandRight,
    //    //12 HipLeft,
    //    //13 KneeLeft,
    //    //14 AnkleLeft,
    //    //15 FootLeft,
    //    //16 HipRight,
    //    //17 KneeRight,
    //    //18 AnkleRight,
    //    //19 FootRight,

    //    public void Update(Skeleton skel)
    //    {

    //        //if (skel.)
    //        //{

    //        //}
    //    }
    //}


    public class ModelDispenser
    {
        public Model[] Models;

        public ModelDispenser(GraphicsControl pipe)
        {
            Models = new Model[]{
                pipe.Content.Load<Model>(@"rustycube"),// parallelepipedo
                pipe.Content.Load<Model>(@"magicsphere"),// sfera
                pipe.Content.Load<Model>(@"football")// inutilizzato
            };
        }
    }

}

