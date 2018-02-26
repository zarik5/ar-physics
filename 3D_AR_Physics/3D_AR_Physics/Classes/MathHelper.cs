using XMatrix = Microsoft.Xna.Framework.Matrix;
using PxMatrix = StillDesign.PhysX.MathPrimitives.Matrix;
using XVector3 = Microsoft.Xna.Framework.Vector3;
using PxVector3 = StillDesign.PhysX.MathPrimitives.Vector3;

namespace AR_Physics.Help
{
    class Math
    {
        public static XMatrix Convert(PxMatrix m)
        {
            XMatrix r = new XMatrix();
            r.M11 = m.M11; r.M12 = m.M12; r.M13 = m.M13; r.M14 = m.M14;
            r.M21 = m.M21; r.M22 = m.M22; r.M23 = m.M23; r.M24 = m.M24;
            r.M31 = m.M31; r.M32 = m.M32; r.M33 = m.M33; r.M34 = m.M34;
            r.M41 = m.M41; r.M42 = m.M42; r.M43 = m.M43; r.M44 = m.M44;
            return r;
        }

        public static PxMatrix Convert(XMatrix m)
        {
            PxMatrix r = new PxMatrix();
            r.M11 = m.M11; r.M12 = m.M12; r.M13 = m.M13; r.M14 = m.M14;
            r.M21 = m.M21; r.M22 = m.M22; r.M23 = m.M23; r.M24 = m.M24;
            r.M31 = m.M31; r.M32 = m.M32; r.M33 = m.M33; r.M34 = m.M34;
            r.M41 = m.M41; r.M42 = m.M42; r.M43 = m.M43; r.M44 = m.M44;
            return r;
        }

        public static XVector3 Convert(PxVector3 v)
        {
            return new XVector3(v.X, v.Y, v.Z);
        }

        public static PxVector3 Convert(XVector3 v)
        {
            return new PxVector3(v.X, v.Y, v.Z);
        }
    }
}
