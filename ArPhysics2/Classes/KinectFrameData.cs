using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using KBody = Microsoft.Kinect.Body;

namespace ArPhysics2
{
    public class KinectFrameData
    {
        public Texture2D ColorTexture, DepthTexture, BodyTexture;
        public CameraSpacePoint[] DepthPoints;
        public Vector3[] DepthSpheresCenters;
        public IReadOnlyDictionary<ulong, KBody> Bodies;
    }
}
