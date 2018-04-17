using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using KPlane = Microsoft.Kinect.Vector4;
using KBody = Microsoft.Kinect.Body;

namespace ArPhysics2
{
    class KinectManager
    {
        public const int NUM_BODIES = 6;
        const int DEPTH_STEP = 10;
        const int SWAP_SIZE = 2;
        const float TARGET_PLANE_M = -1.5f;
        static readonly Vector3 INVALID_POINT = new Vector3(float.NegativeInfinity); 
        
        public KinectFrameData CurrentFrames => swapChain.Peek();
        public Matrix ViewMatrix { get; private set; }
        public int DepthSpheresNumber => horizNumSpheres * vertNumSpheres;
        public float DepthSpheresSpacingAt1M => (float)Math.Sin(MathHelper.ToRadians(
            kinect.DepthFrameSource.FrameDescription.VerticalFieldOfView)) / vertNumSpheres;
        
        KinectSensor kinect;
        MultiSourceFrameReader frameReader;
        GraphicsDevice gdevice;

        Queue<KinectFrameData> swapChain = new Queue<KinectFrameData>(SWAP_SIZE);

        byte[] tempColorBuffer, tempBodyIdxBuffer;
        ushort[] tempDepthBuffer;
        KBody[] tempBodyBuffer;
        CameraSpacePoint[] tempCameraPoints;
        Size colorFrameSize, depthFrameSize;

        bool shouldUpdateColor = true;
        bool shouldUpdateBodyIndex = false;
        // depth and skeleton/body are needed in every game mode

        Action<KPlane> planeFoundCallback = null;
        bool calibrated = false;

        int horizNumSpheres, vertNumSpheres;

        public void Initialize(GraphicsDevice gdevice)
        {
            this.gdevice = gdevice;
            kinect = KinectSensor.GetDefault();
            frameReader = kinect.OpenMultiSourceFrameReader(FrameSourceTypes.Color
                | FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.BodyIndex);
            frameReader.MultiSourceFrameArrived += FramesArrived;

            var colorDesc = kinect.ColorFrameSource.FrameDescription;
            var depthDesc = kinect.DepthFrameSource.FrameDescription;
            var bodyIdxDesc = kinect.BodyIndexFrameSource.FrameDescription;
            colorFrameSize = new Size(colorDesc.Width, colorDesc.Height);
            depthFrameSize = new Size(depthDesc.Width, depthDesc.Height);

            tempCameraPoints = new CameraSpacePoint[depthDesc.LengthInPixels];

            horizNumSpheres = depthDesc.Width / DEPTH_STEP; // implicit int floor
            vertNumSpheres = depthDesc.Height / DEPTH_STEP;
            
            for (int i = 0; i < SWAP_SIZE; i++)
            {
                swapChain.Enqueue(new KinectFrameData
                {
                    ColorTexture = new Texture2D(gdevice, colorDesc.Width, colorDesc.Height,
                    false, SurfaceFormat.Bgra32),
                    DepthTexture = new Texture2D(gdevice, depthDesc.Width, depthDesc.Height,
                        false, SurfaceFormat.Bgra4444), // must be reassembled in shader
                    BodyTexture = new Texture2D(gdevice, bodyIdxDesc.Width, bodyIdxDesc.Height,
                        false, SurfaceFormat.Alpha8),
                    DepthSpheresCenters = new Vector3[horizNumSpheres * vertNumSpheres],
                    Bodies = new Dictionary<ulong, KBody>()
                });
            }

            // I cannot copy directly kinect frames to textures
            // because the language does not allow casts from unsafe pointer to managed array,
            // so I use temporary buffers.
            tempColorBuffer = new byte[colorDesc.LengthInPixels * 4];
            tempDepthBuffer = new ushort[depthDesc.LengthInPixels];
            tempBodyIdxBuffer = new byte[bodyIdxDesc.LengthInPixels];
            tempBodyBuffer = new KBody[NUM_BODIES];
        }

        public void SetGameMode(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.SANDBOX:
                    shouldUpdateColor = true;
                    shouldUpdateBodyIndex = true;
                    break;
                case GameMode.HIW_DEPTH:
                    shouldUpdateColor = false;
                    shouldUpdateBodyIndex = false;
                    break;
                case GameMode.HIW_SEGMENT:
                case GameMode.HIW_PHYSICS:
                    shouldUpdateColor = false;
                    shouldUpdateBodyIndex = true;
                    break;
            }
        }

        void FramesArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var multiFrame = e.FrameReference.AcquireFrame();
            if (multiFrame == null)
                return;

            using (var bodyFrame = multiFrame.BodyFrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    bodyFrame.GetAndRefreshBodyData(tempBodyBuffer);
                    var plane = bodyFrame.FloorClipPlane;
                    if (plane.W != 0 && !calibrated)
                    {
                        planeFoundCallback?.Invoke(plane);
                        calibrated = true;
                    }
                }
            }
            using (var depthFrame = multiFrame.DepthFrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    using (var depthBuffer = depthFrame.LockImageBuffer())
                        depthFrame.CopyFrameDataToArray(tempDepthBuffer);
                }
            }

            if (shouldUpdateColor)
            {
                using (var colorFrame = multiFrame.ColorFrameReference.AcquireFrame())
                {
                    if (colorFrame != null)
                    {
                        using (var colorBuffer = colorFrame.LockRawImageBuffer())
                            colorFrame.CopyConvertedFrameDataToArray(tempColorBuffer, ColorImageFormat.Bgra);
                    }
                }
            }
            if (shouldUpdateBodyIndex)
            {
                using (var bodyIndexFrame = multiFrame.BodyIndexFrameReference.AcquireFrame())
                {
                    if (bodyIndexFrame != null)
                    {
                        using (var bodyIdxBuffer = bodyIndexFrame.LockImageBuffer())
                            bodyIndexFrame.CopyFrameDataToArray(tempBodyIdxBuffer);
                    }
                }
            }
            
            //Task.Run(() => UpdateFrames());
            UpdateFrames();
        }

        void UpdateFrames()
        {
            var kframes = swapChain.ElementAt(SWAP_SIZE - 1); // back buffer

            var dict = new Dictionary<ulong, KBody>(NUM_BODIES);
            foreach (var body in (from body in tempBodyBuffer where body != null && body.IsTracked select body))
                dict.Add(body.TrackingId, body);
            kframes.Bodies = dict;

            kinect.CoordinateMapper.MapDepthFrameToCameraSpace(tempDepthBuffer, tempCameraPoints);

            lock (kframes.DepthSpheresCenters)
            {
                for (int y = 0; y < vertNumSpheres; y++)
                {
                    for (int x = 0; x < horizNumSpheres; x++)
                    {
                        var idx = y * DEPTH_STEP * depthFrameSize.Width + x * DEPTH_STEP;
                        // make all body index points invalid
                        var kPt = tempBodyIdxBuffer[idx] == 0xFF ? tempCameraPoints[idx].ToXNA() : INVALID_POINT;
                        kframes.DepthSpheresCenters[y * horizNumSpheres + x] = kPt;
                    }
                }
            }
            
            lock (kframes.DepthTexture)
                kframes.DepthTexture.SetData(tempDepthBuffer);
            
            if (shouldUpdateColor)
            {
                lock (kframes.ColorTexture)
                    kframes.ColorTexture.SetData(tempColorBuffer);
            }
            if (shouldUpdateBodyIndex)
            {
                lock (kframes.BodyTexture)
                    kframes.BodyTexture.SetData(tempBodyIdxBuffer);
            }

            swapChain.Enqueue(swapChain.Dequeue()); // swap front/back buffers
        }


        public (Texture2D, Texture2D, DepthSpacePoint[], bool) CreateMapperTextures(Matrix projMatrix, Viewport viewport)
        {
            var w = viewport.Width;
            var h = viewport.Height;
            var colorPoints = new ColorSpacePoint[w * h];
            var depthPoints = new DepthSpacePoint[w * h];
            var colorMapper = new Texture2D(gdevice, w, h, false, SurfaceFormat.Vector2); // two channel float32
            var depthMapper = new Texture2D(gdevice, w, h, false, SurfaceFormat.Vector2);

            var targetProjPlane = viewport.Project(new Vector3(0, 0, TARGET_PLANE_M),
                projMatrix, Matrix.Identity, Matrix.Identity).Z;
            var cameraPoints = new CameraSpacePoint[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var point = viewport.Unproject(new Vector3(x, y, targetProjPlane),
                        projMatrix, Matrix.Identity, Matrix.Identity);
                    cameraPoints[y * w + x] = new CameraSpacePoint
                    {
                        X = point.X,
                        Y = point.Y,
                        Z = -point.Z
                    };
                }
            }

            //sometimes the coordinate mapper returns invalid buffers, retry until a valid buffer is returned
            int colorTimeoutCounter = 0;
            do
            {
                kinect.CoordinateMapper.MapCameraPointsToColorSpace(cameraPoints, colorPoints);
                colorTimeoutCounter++;
            } while (float.IsInfinity(colorPoints[w * h / 2 + w / 2].X) && colorTimeoutCounter < 20);
            int depthTimeoutCounter = 0;
            do
            {
                kinect.CoordinateMapper.MapCameraPointsToDepthSpace(cameraPoints, depthPoints);
                depthTimeoutCounter++;
            } while (float.IsInfinity(depthPoints[w * h / 2 + w / 2].X) && depthTimeoutCounter < 20);

            var colorStartX = (colorFrameSize.Width - w) / 2;
            var colorStartY = (colorFrameSize.Height - h) / 2;
            var depthStartX = (depthFrameSize.Width - w) / 2;
            var depthStartY = (depthFrameSize.Height - h) / 2;
            for (int i = 0; i < colorPoints.Length; i++)
            {
                colorPoints[i].X /= colorFrameSize.Width;
                colorPoints[i].Y /= colorFrameSize.Height;
                depthPoints[i].X /= depthFrameSize.Width;
                depthPoints[i].Y /= depthFrameSize.Height;
            }
            colorMapper.SetData(colorPoints);
            depthMapper.SetData(depthPoints);
            return (colorMapper, depthMapper, depthPoints,
                colorTimeoutCounter < 20 && depthTimeoutCounter < 20);
        }

        public void Calibrate(Action<float, float, Matrix> callback)
        {
            planeFoundCallback = plane =>
            {
                var planeHeight = plane.W;
                var view = Matrix.CreateRotationX(-(float)Math.Atan(plane.Z / plane.Y));
                var minZ = float.PositiveInfinity;
                foreach (var body in (from body in CurrentFrames.Bodies
                                      where body.Value.IsTracked
                                      select body))
                {
                    //todo use right math
                    minZ = MathHelper.Min(minZ, body.Value.Joints[JointType.SpineMid].Position.Z);
                }
                callback(planeHeight, minZ, view);
            };
            calibrated = false;
        }

        public void Dispose()
        {
            frameReader?.Dispose();
            kinect?.Close();
        }
    }
}
