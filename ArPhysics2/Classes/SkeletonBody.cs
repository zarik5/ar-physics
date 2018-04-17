using BulletSharp;
using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using J = Microsoft.Kinect.JointType;
using KBody = Microsoft.Kinect.Body;

namespace ArPhysics2
{
    class SkeletonBody
    {
        const float HAND_COLLIDER_RADIUS_M = .07f;
        const float CREATING_SIZE_MUL = 0.6f;
        const float STROKE_DENSITY_KG_OVER_M3 = 10f;
        static readonly Matrix INIT_POSE = Matrix.CreateTranslation(new Vector3(0, 0, 10));

        static readonly (TrackingState, Color)[] TRACKING_COLORS = new[]
        {
            (TrackingState.NotTracked, Color.TRANSPARENT),
            (TrackingState.Inferred, Color.DISABLED_GRAY),
            (TrackingState.Tracked, Color.GRAY),
        };

        static readonly (HandState, Color)[] HAND_COLORS = new[]
        {
            (HandState.NotTracked, Color.DISABLED_GRAY),
            (HandState.Unknown, Color.TRANSPARENT_GRAY),
            (HandState.Open, Color.GREEN),
            (HandState.Closed, Color.RED),
            (HandState.Lasso, Color.BLUE),
        };

        static readonly (J targetJoint, J parentJoint, Vector2 thickness)[] BODY_PARTS = new[]
        { 
            //spine
            (J.SpineMid, J.SpineBase, new Vector2(.30f, .20f)),
            (J.SpineShoulder, J.SpineMid, new Vector2(.32f, .18f)),
            (J.Neck, J.SpineShoulder, new Vector2(.10f, .10f)),

            //arms
            (J.ShoulderLeft, J.SpineShoulder, new Vector2(.05f, .20f)),
            (J.ShoulderRight, J.SpineShoulder, new Vector2(.05f, .20f)),
            (J.ElbowLeft, J.ShoulderLeft, new Vector2(.10f, .11f)),
            (J.ElbowRight, J.ShoulderRight, new Vector2(.10f, .11f)),
            (J.WristLeft, J.ElbowLeft, new Vector2(.06f, .09f)),
            (J.WristRight, J.ElbowRight, new Vector2(.06f, .09f)),
        
            //legs
            (J.KneeLeft, J.HipLeft, new Vector2(.17f, .15f)),
            (J.KneeRight, J.HipRight, new Vector2(.17f, .15f)),
            (J.AnkleLeft, J.KneeLeft, new Vector2(.12f, .12f)),
            (J.AnkleRight, J.KneeRight, new Vector2(.12f, .12f)),
        };

        static readonly (J centerJoint, J parentJoint, Vector3 size)[] EXTREMITIES = new[]
        {
            (J.Head, J.Neck, new Vector3(.17f)),
            (J.FootLeft, J.AnkleLeft, new Vector3(.20f, .08f, .10f)),
            (J.FootRight, J.AnkleRight, new Vector3(.20f, .08f, .10f)),
        };

        private Matrix view = Matrix.Identity;
        private Matrix invView = Matrix.Identity;
        public Matrix View
        {
            set
            {
                view = value;
                invView = Matrix.Invert(value);
                bodyParts.ToList().ForEach(pair => pair.Value.Item2.View = value);
                extremities.ToList().ForEach(pair => pair.Value.Item2.View = value);
                hands.ToList().ForEach(pair => pair.Value.HandBody.View = value);
            }
        }

        private Matrix projection = Matrix.Identity;
        public Matrix Projection
        {
            set
            {
                projection = value;
                bodyParts.ToList().ForEach(pair => pair.Value.Item2.Projection = value);
                extremities.ToList().ForEach(pair => pair.Value.Item2.Projection = value);
                hands.ToList().ForEach(pair => pair.Value.HandBody.Projection = value);
            }
        }

        private class Hand
        {
            public J Wrist, HandTip;
            public Body HandBody;
            public PairCachingGhostObject Collider;
            public bool Grabbing, Drawing;
            public List<(RigidBody, Matrix relativePose, float oldMass)> GrabbedBodies =
                new List<(RigidBody, Matrix, float)>();
            public BasicEffect DrawingEffect;
            //public Body LastStroke;
            public Vector3? LastTipPos;
        }

        public DynamicsWorld world;

        // drawing
        Model boxModel;
        IReadOnlyDictionary<TrackingState, BasicEffect> bodyPartEffects;
        IReadOnlyDictionary<HandState, BasicEffect> handEffects;
        Effect depthEffect;
        Action<Body> getDrawStroke;

        // bodies
        Dictionary<J, (J parentJoint, Body)> bodyParts = new Dictionary<J, (J, Body)>();
        Dictionary<J, (J parentJoint, Body)> extremities = new Dictionary<J, (J, Body)>();
        Dictionary<J, Hand> hands = new Dictionary<J, Hand>();

        // creating object
        Body creatingObject;
        bool multiDimensionResizeable;
        float density;
        Vector3 gravity;


        public SkeletonBody(DynamicsWorld world, Effect depthEffect,
            IReadOnlyDictionary<Shape, Model> models,
            IReadOnlyDictionary<Color, BasicEffect> colorEffects, Action<Body> getDrawStroke)
        {
            this.world = world;
            this.depthEffect = depthEffect;
            this.getDrawStroke = getDrawStroke;
            boxModel = models[Shape.BOX];
            bodyPartEffects = TRACKING_COLORS.MapToDictionary(color => colorEffects[color]);
            handEffects = HAND_COLORS.MapToDictionary(color => colorEffects[color]);

            bodyParts = (from p in BODY_PARTS select (p.targetJoint, p))
                .MapToDictionary(p => (p.parentJoint, new Body(world, boxModel, Shape.BOX, INIT_POSE, 0)
                {
                    ColorEffect = bodyPartEffects[TrackingState.NotTracked],
                    Scale = new Vector3(p.thickness.X, 1, p.thickness.Y),
                    View = view,
                    Projection = projection,
                }));

            extremities = (from e in EXTREMITIES select (e.centerJoint, e))
                .MapToDictionary(p => (p.parentJoint, new Body(world, boxModel, Shape.BOX, INIT_POSE, 0)
                {
                    ColorEffect = bodyPartEffects[TrackingState.NotTracked],
                    Scale = p.size,
                    View = view,
                    Projection = projection,
                }));

            // hands
            foreach (var (handType, handTip, wrist) in new[] {
                (J.HandLeft, J.HandTipLeft, J.WristLeft),
                (J.HandRight, J.HandTipRight, J.WristRight) })
            {
                var handBody = new Body(world, boxModel, Shape.BOX, INIT_POSE, 0)
                {
                    PhysicsEnabled = false,
                    ColorEffect = handEffects[HandState.NotTracked],
                    Scale = new Vector3(.04f, 1, .10f),
                };
                var collider = new PairCachingGhostObject()
                {
                    CollisionShape = new SphereShape(HAND_COLLIDER_RADIUS_M),
                    WorldTransform = Matrix.Identity,
                };
                collider.CollisionFlags |= CollisionFlags.NoContactResponse;
                world.AddCollisionObject(collider);
                hands[handType] = new Hand
                {
                    HandTip = handTip,
                    Wrist = wrist,
                    HandBody = handBody,
                    Collider = collider,
                    Grabbing = false,
                    Drawing = false,
                };
            }
        }

        public void Update(KBody targetBody)
        {
            //update body parts
            foreach (var (toJointKey, (fromJointKey, bodyPart)) in bodyParts)
            {
                var fromJoint = targetBody.Joints[fromJointKey];
                var toJoint = targetBody.Joints[toJointKey];
                var fromCoord = fromJoint.Position.ToXNA();
                var toCoord = toJoint.Position.ToXNA();

                var partLength = Vector3.Distance(toCoord, fromCoord);
                bodyPart.Scale = new Vector3(bodyPart.Scale.X, partLength, bodyPart.Scale.Z);

                // position on bone center
                bodyPart.Pose = Matrix.CreateFromQuaternion(targetBody.JointOrientations[toJointKey].Orientation.ToXNA())
                    * Matrix.CreateTranslation((fromCoord + toCoord) / 2) * invView;

                bodyPart.ColorEffect = bodyPartEffects[toJoint.TrackingState];
                bodyPart.PhysicsEnabled = toJoint.TrackingState == TrackingState.Tracked;
            }

            // update extremities
            foreach (var (centerJointKey, (orientJointKey, bodyPart)) in extremities)
            {
                var centerJoint = targetBody.Joints[centerJointKey];
                var centerJointCoord = centerJoint.Position.ToXNA();

                // there is no orientation information for extremities
                bodyPart.Pose = Matrix.CreateFromQuaternion(
                    targetBody.JointOrientations[orientJointKey].Orientation.ToXNA())
                    * Matrix.CreateTranslation(centerJointCoord) * invView;

                bodyPart.ColorEffect = bodyPartEffects[centerJoint.TrackingState];
                bodyPart.PhysicsEnabled = centerJoint.TrackingState == TrackingState.Tracked;
            }

            //update hands
            foreach (var (handType, hand) in hands)
            {
                var wristKey = hand.Wrist;
                var handTipKey = hand.HandTip;
                var handBody = hand.HandBody;

                var wristPos = targetBody.Joints[wristKey].Position.ToXNA();
                var handTipPos = targetBody.Joints[handTipKey].Position.ToXNA();

                var handLength = Vector3.Distance(handTipPos, wristPos);
                handBody.Scale = new Vector3(handBody.Scale.X, handLength, handBody.Scale.Z);

                // rotate on wrist, then traslate
                handBody.Pose = Matrix.CreateTranslation(new Vector3(0, handLength / 2, 0))
                    * Matrix.CreateFromQuaternion(targetBody.JointOrientations[handType].Orientation.ToXNA())
                    * Matrix.CreateTranslation(wristPos) * invView;

                var handState = handType == J.HandLeft ? targetBody.HandLeftState : targetBody.HandRightState;
                handBody.ColorEffect = handEffects[handState];

                hand.Collider.WorldTransform = handBody.Pose;

                if (hand.Grabbing)
                {
                    foreach (var (rigidBody, relativePose, _) in hand.GrabbedBodies)
                    {
                        var pose = relativePose * Matrix.CreateTranslation(handBody.Pose.Translation);
                        rigidBody.MotionState.WorldTransform = pose;
                        rigidBody.WorldTransform = pose;
                    }
                }

                if (hand.Drawing)
                {
                    if (hand.LastTipPos == null)
                        hand.LastTipPos = handTipPos;
                    var lastTipPose = (Vector3)hand.LastTipPos;
                    var translation = handTipPos - lastTipPose;
                    var translDist = translation.Length();
                    if (translDist > VirtualScene.MAX_LENGTH_STROKE)
                    {
                        var rotationAxis = Vector3.Cross(Vector3.Up, translation);
                        var angleBetween = (float)(Math.Acos(Vector3.Dot(Vector3.Up, translation) / translDist));
                        var pose = Matrix.CreateFromAxisAngle(Vector3.Normalize(rotationAxis), angleBetween)
                            * Matrix.CreateTranslation((handTipPos + lastTipPose) / 2) * invView;
                        var strokeBody = new Body(world, boxModel, Shape.BOX, pose, STROKE_DENSITY_KG_OVER_M3)
                        {
                            ColorEffect = hand.DrawingEffect,
                            DepthEffect = depthEffect,
                            Scale = new Vector3(VirtualScene.STROKE_THICKNESS_M,
                                translDist + VirtualScene.STROKE_THICKNESS_M / 2,
                                VirtualScene.STROKE_THICKNESS_M),
                            View = view,
                            Projection = projection,

                            Gravity = Vector3.Zero,
                        };
                        strokeBody.PhysicsEnabled = false; // make sure to set this property at last, to allow gravity setting;
                        //if (hand.LastStroke != null)
                        //{
                        //    strokeBody.SetCollisionWithBody(hand.LastStroke, false);
                        //}
                        //hand.LastStroke = strokeBody;
                        getDrawStroke(strokeBody);
                        hand.LastTipPos = handTipPos;
                    }
                }
                else // if not drawing
                {
                    handBody.PhysicsEnabled = handState == HandState.Open || handState == HandState.Closed;
                    // if unknown, not tracked or lasso, disable physics
                }
            }

            if (creatingObject != null)
            {
                var leftPoint = targetBody.Joints[J.HandLeft].Position.ToXNA();
                var rightPoint = targetBody.Joints[J.HandRight].Position.ToXNA();
                creatingObject.Pose = Matrix.CreateTranslation((leftPoint + rightPoint) / 2) * invView;
                var rawSize = (leftPoint - rightPoint) * CREATING_SIZE_MUL;
                var size = multiDimensionResizeable
                    ? new Vector3(Math.Abs(rawSize.X), Math.Abs(rawSize.Y), Math.Abs(rawSize.Z))
                    : new Vector3(rawSize.Length());
                creatingObject.Scale = Vector3.Clamp(size, new Vector3(.10f), new Vector3(1f));
            }

        }

        public void Draw(bool physics, bool transpFlag = false)
        {
            if (physics)
            {
                foreach (var (_, (_, bodyPart)) in bodyParts)
                    bodyPart.Draw(transpFlag);
                foreach (var (_, (_, bodyPart)) in extremities)
                    bodyPart.Draw(transpFlag);
                foreach (var (_, hand) in hands)
                    hand.HandBody.Draw(transpFlag);
            }
            creatingObject?.Draw(transpFlag);
        }

        public void DrawDepth()
        {
            creatingObject?.DrawDepth();
        }

        // interactions:

        public void BeginCreatingObject(Body creatingObject, bool multiDimensionResizeable)
        {
            // create if not grabbing anything
            if (this.creatingObject == null && hands.Aggregate(true, (acc, pair) =>
                acc && (pair.Value.Grabbing == false || pair.Value.GrabbedBodies.Count == 0)))
            {
                this.creatingObject = creatingObject;
                this.multiDimensionResizeable = multiDimensionResizeable;
                density = creatingObject.Density;
                gravity = creatingObject.Gravity;
                creatingObject.Density = 0;
                creatingObject.Gravity = Vector3.Zero;
            }
        }

        public Body FinishCreatingObject()
        {
            var createdObj = creatingObject; // can be null
            if (createdObj != null)
            {
                createdObj.Density = density;
                createdObj.Gravity = gravity;

            }
            creatingObject = null;
            return createdObj;
        }

        public void SetGrabState(J handJoint, bool enabled)
        {
            var hand = hands[handJoint];
            hand.Grabbing = enabled;
            if (enabled)
            {
                ClearGrabList(hand);
                for (int i = 0; i < hand.Collider.NumOverlappingObjects; i++)
                {
                    if (hand.Collider.GetOverlappingObject(i) is RigidBody obj)
                    {
                        // grab all non kinematic bodies.
                        if (obj.InvMass != 0 && hand.HandBody.HasRigidBody(obj) == false
                            && bodyParts[hand.Wrist].Item2.HasRigidBody(obj) == false)
                        {
                            hand.GrabbedBodies.Add((obj, obj.MotionState.WorldTransform
                                * Matrix.Invert(Matrix.CreateTranslation(hand.HandBody.Pose.Translation)),
                                1f / obj.InvMass));
                            obj.SetMassProps(0, Vector3.Zero);
                        }
                    }
                }
            }
            else
            {
                ClearGrabList(hand);
            }
        }

        void ClearGrabList(Hand hand)
        {
            // restore mass property, wake and remove
            foreach (var (rigidBody, _, mass) in hand.GrabbedBodies)
            {
                rigidBody.SetMassProps(mass, rigidBody.CollisionShape.CalculateLocalInertia(mass));
                rigidBody.Activate();
            }
            hand.GrabbedBodies.Clear();
        }

        public void SetDrawingState(J handJoint, bool enabled, BasicEffect effect = null)
        {
            var hand = hands[handJoint];
            hand.Drawing = enabled;
            hand.DrawingEffect = effect;
            hand.LastTipPos = null;
        }

        public void Dispose()
        {
            SetGrabState(J.HandLeft, false);
            SetGrabState(J.HandRight, false);
            SetDrawingState(J.HandLeft, false);
            SetDrawingState(J.HandRight, false);
            foreach (var (_, (_, bodyPart)) in bodyParts)
                bodyPart.Dispose();
            bodyParts.Clear();
            foreach (var (_, (_, bodyPart)) in extremities)
                bodyPart.Dispose();
            extremities.Clear();
            foreach (var (_, hand) in hands)
            {
                hand.HandBody.Dispose();
                world.RemoveCollisionObject(hand.Collider);
                hand.Collider.Dispose();
            }
            hands.Clear();
            creatingObject?.Dispose();
        }
    }
}
