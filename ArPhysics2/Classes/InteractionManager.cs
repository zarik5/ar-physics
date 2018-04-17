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
    public class InteractionManager
    {
        const float ENGAGED_SPINE_OFFSET = -0.20f;
        static readonly TimeSpan MAX_IDLE_SPAN = new TimeSpan(0, minutes: 1, 0);
        static readonly TimeSpan EXPIRE_STATE_SPAN = new TimeSpan(0, 0, 0, 0, milliseconds: 900);
        static readonly TimeSpan ANTI_SPAM_SPAN = new TimeSpan(0, 0, 0, 0, milliseconds: 400);

        class UserHandsState
        {
            public HandState LeftState, RightState;
            public DateTime LeftChangeTime, RightChangeTime; // used to retain last gesture if current gesture is unknown
                                                             // because out-of-the-box gesture recognition is glitchy
        }

        public bool AutoReset { get; set; }


        Dictionary<ulong, UserHandsState> usersHands;

        Viewport viewport;
        Matrix projetion;
        Rectangle gameSpace;

        // mono user interaction state.

        private enum UIInterState
        {
            IDLE,
            PEEKING_MENU,
            GRABBING_MENU,
            REACHING_MARGIN,
        }

        UIInterState uiInterState;
        ulong uiInterUserId = 0;
        J uiInterHand = 0;
        GameMode gameMode;

        DateTime lastInteractionTime = DateTime.Now;
        
        Action<ulong, InteractionType, J> interactionCb;
        Action<ulong, InteractionType, J> interCbWithIdleRefresh;

        
        public void Initialize(Action<ulong, InteractionType, J> interactionCb)
        {
            this.interactionCb = interactionCb;
            interCbWithIdleRefresh = (id, inter, hand) =>
            {
                lastInteractionTime = DateTime.Now;
                if (gameMode != GameMode.NONE)
                    interactionCb(id, inter, hand);
            };
            Reset();
        }

        public void UpdateViewport(Viewport vp, Matrix proj, Rectangle gameSpace)
        {
            this.gameSpace = gameSpace;
            viewport = vp;
            projetion = proj;
        }

        public void Reset()
        {
            usersHands = new Dictionary<ulong, UserHandsState>();
            uiInterState = UIInterState.IDLE;
        }

        public void SetGameMode(GameMode mode)
        {
            gameMode = mode;
            lastInteractionTime = DateTime.Now;
        }

        public void Update(IReadOnlyDictionary<ulong, KBody> bodies)
        {
            // handle UI user changed
            var oldUiUserId = uiInterUserId;
            var leastDist = float.PositiveInfinity;
            foreach (var (id, user) in bodies)
            {
                var userPos = user.Joints[J.SpineMid].Position;
                var curDist = Math.Abs(userPos.X) + Math.Abs(userPos.Z);
                if (curDist < leastDist)
                {
                    uiInterUserId = id;
                    leastDist = curDist;
                }
            }
            if (oldUiUserId != uiInterUserId && uiInterState != UIInterState.IDLE)
            {
                if (uiInterState == UIInterState.PEEKING_MENU)
                    interactionCb(oldUiUserId, InteractionType.CANCEL_PEEK_MENU, 0); // 0 -> don't care
                else if (uiInterState == UIInterState.GRABBING_MENU)
                    interactionCb(oldUiUserId, InteractionType.RELEASE_MENU_CANCEL, 0);
                else if (uiInterState == UIInterState.REACHING_MARGIN)
                    interactionCb(oldUiUserId, InteractionType.END_REACH_MARGIN, 0);
                uiInterState = UIInterState.IDLE;
            }

            var targetIds = bodies.Keys;
            for (int i = usersHands.Count - 1; i >= 0; i--)
            {
                var (id, oldHands) = usersHands.ElementAt(i);
                if (targetIds.Contains(id) == false)
                    usersHands.Remove(id);
                //do not raise release/end gesture event, respective user has already been disposed in the virtual scene
            }

            foreach (var (id, user) in bodies)
            {
                if (usersHands.ContainsKey(id) == false)
                {
                    usersHands[id] = new UserHandsState()
                    {
                        LeftState = HandState.Unknown,
                        RightState = HandState.Unknown,
                        LeftChangeTime = DateTime.Now,
                        RightChangeTime = DateTime.Now,
                    };
                }

                DetectEvent(user);
            }

            if (AutoReset && DateTime.Now - lastInteractionTime > MAX_IDLE_SPAN)
            {
                lastInteractionTime = DateTime.Now;
                interactionCb(0, InteractionType.IDLE_TIMEOUT, 0);
            }
        }

        void DetectEvent(KBody user)
        {
            var id = user.TrackingId;
            var oldHands = usersHands[id];
            var now = DateTime.Now;
            var clippedEdges = (user.ClippedEdges & (FrameEdges.Right | FrameEdges.Left | FrameEdges.Top)) != 0;
            var lastLeftEventElapsed = now - oldHands.LeftChangeTime;
            var lastRightEventElapsed = now - oldHands.RightChangeTime;

            var topHeadPos = user.Joints[J.Head].Position;
            var spineMidPos = user.Joints[J.SpineMid].Position;
            var leftCoord = CameraPointToScreen(user.Joints[J.HandLeft].Position);
            var rightCoord = CameraPointToScreen(user.Joints[J.HandRight].Position);
            var leftWristPos = user.Joints[J.WristLeft].Position;
            var rightWristPos = user.Joints[J.WristRight].Position;
            var leftHandTracked = user.Joints[J.HandLeft].TrackingState == TrackingState.Tracked;
            var rightHandTracked = user.Joints[J.HandRight].TrackingState == TrackingState.Tracked;
            var leftEngaged = leftHandTracked && leftWristPos.Y > spineMidPos.Y + ENGAGED_SPINE_OFFSET;
            var rightEngaged = rightHandTracked && rightWristPos.Y > spineMidPos.Y + ENGAGED_SPINE_OFFSET;


            //Hand shape gestures:

            //both hands logic:
            if (lastLeftEventElapsed > ANTI_SPAM_SPAN || lastRightEventElapsed > ANTI_SPAM_SPAN)
            {
                var leftClosed = user.HandLeftState == HandState.Closed;
                var rightClosed = user.HandRightState == HandState.Closed;
                var oldLeftClosed = oldHands.LeftState == HandState.Closed;
                var oldRightClosed = oldHands.RightState == HandState.Closed;

                if (leftEngaged && rightEngaged && leftClosed && rightClosed
                    && (!oldLeftClosed || !oldRightClosed))
                {
                    interCbWithIdleRefresh(id, InteractionType.BEGIN_CREATING_OBJECT, 0);
                }
                else if (oldLeftClosed && oldRightClosed
                    && (!leftClosed || !rightClosed))
                {
                    interCbWithIdleRefresh(id, InteractionType.FINISH_CREATING_OBJECT, 0);
                }

                // old hand state is updated below
            }

            //left hand logic:
            if (lastLeftEventElapsed > ANTI_SPAM_SPAN)
            {
                if (user.HandLeftState == HandState.Unknown || user.HandLeftState == HandState.NotTracked)
                {
                    //hand gesture expired
                    if (oldHands.LeftState != HandState.Unknown
                        && now - oldHands.LeftChangeTime > EXPIRE_STATE_SPAN)
                    {
                        ProcessHandGesture(user, J.HandLeft, user.HandLeftState, oldHands.LeftState);
                        oldHands.LeftState = HandState.Unknown;
                    }
                }
                else if (user.HandLeftState != oldHands.LeftState
                        && (user.HandLeftState != HandState.Lasso || leftEngaged))
                {
                    ProcessHandGesture(user, J.HandLeft, user.HandLeftState, oldHands.LeftState);
                    oldHands.LeftState = user.HandLeftState;
                    oldHands.LeftChangeTime = now;
                }
            }

            //right hand logic:
            if (lastRightEventElapsed > ANTI_SPAM_SPAN)
            {
                if (user.HandRightState == HandState.Unknown || user.HandRightState == HandState.NotTracked)
                {
                    //hand gesture expired
                    if (oldHands.RightState != HandState.Unknown
                        && now - oldHands.RightChangeTime > EXPIRE_STATE_SPAN)
                    {
                        ProcessHandGesture(user, J.HandRight, user.HandRightState, oldHands.RightState);
                        oldHands.RightState = HandState.Unknown;
                    }
                }
                else if (user.HandRightState != oldHands.RightState
                        && (user.HandRightState != HandState.Lasso || rightEngaged))
                {
                    ProcessHandGesture(user, J.HandRight, user.HandRightState, oldHands.RightState);
                    oldHands.RightState = user.HandRightState;
                    oldHands.RightChangeTime = now;
                }
            }

            if (id == uiInterUserId)
            {
                // menu peeking gestures:

                //left hand logic:
                if (leftEngaged && !clippedEdges
                    && leftWristPos.Y > topHeadPos.Y && user.HandLeftState == HandState.Open)
                {
                    if (uiInterState == UIInterState.IDLE)
                    {
                        uiInterState = UIInterState.PEEKING_MENU;
                        uiInterHand = J.HandLeft;
                        interCbWithIdleRefresh(id, InteractionType.PEEK_MENU, J.HandLeft);
                    }
                }
                else if (uiInterState == UIInterState.PEEKING_MENU && uiInterHand == J.HandLeft)
                {
                    uiInterState = UIInterState.IDLE;
                    interCbWithIdleRefresh(id, InteractionType.CANCEL_PEEK_MENU, J.HandLeft);
                }

                //right hand logic:
                if (rightEngaged && !clippedEdges
                    && rightWristPos.Y > topHeadPos.Y && user.HandRightState == HandState.Open)
                {
                    if (uiInterState == UIInterState.IDLE)
                    {
                        uiInterState = UIInterState.PEEKING_MENU;
                        uiInterHand = J.HandRight;
                        interCbWithIdleRefresh(id, InteractionType.PEEK_MENU, J.HandRight);
                    }
                }
                else if (uiInterState == UIInterState.PEEKING_MENU && uiInterHand == J.HandRight)
                {
                    uiInterState = UIInterState.IDLE;
                    interCbWithIdleRefresh(id, InteractionType.CANCEL_PEEK_MENU, J.HandRight);
                }

                // Margin reaching logic:

                //left
                if (leftHandTracked && (leftCoord.X < gameSpace.Left
                        || (user.ClippedEdges & FrameEdges.Left) != 0))
                {
                    if (leftEngaged && uiInterState == UIInterState.IDLE) // put in nested if to allow keeping margin interaction if hand is lowered
                    {
                        uiInterState = UIInterState.REACHING_MARGIN;
                        uiInterHand = J.HandLeft;
                        interCbWithIdleRefresh(id, InteractionType.BEGIN_REACH_MARGIN, J.HandLeft);
                    }
                }
                else if (uiInterState == UIInterState.REACHING_MARGIN && uiInterHand == J.HandLeft)
                {
                    uiInterState = UIInterState.IDLE;
                    interCbWithIdleRefresh(id, InteractionType.END_REACH_MARGIN, J.HandLeft);
                }

                //right
                if (rightHandTracked && (rightCoord.X > gameSpace.Right
                        || (user.ClippedEdges & FrameEdges.Right) != 0))
                {
                    if (rightEngaged && uiInterState == UIInterState.IDLE)
                    {
                        uiInterState = UIInterState.REACHING_MARGIN;
                        uiInterHand = J.HandRight;
                        interCbWithIdleRefresh(id, InteractionType.BEGIN_REACH_MARGIN, J.HandRight);
                    }
                }
                else if (uiInterState == UIInterState.REACHING_MARGIN && uiInterHand == J.HandRight)
                {
                    uiInterState = UIInterState.IDLE;
                    interCbWithIdleRefresh(id, InteractionType.END_REACH_MARGIN, J.HandRight);
                }
            }

        }

        void ProcessHandGesture(KBody user, J hand, HandState curState, HandState oldState)
        {
            var id = user.TrackingId;
            var uiInteracting = uiInterState != UIInterState.IDLE && uiInterUserId == id && uiInterHand == hand;
            var shoulderPos = user.Joints[J.SpineShoulder].Position;

            //filter on old state
            if (oldState == HandState.Closed)
            {
                if (uiInterState == UIInterState.GRABBING_MENU && uiInteracting)
                {
                    uiInterState = UIInterState.IDLE;
                    if (user.Joints[hand].Position.Y < shoulderPos.Y)
                        interCbWithIdleRefresh(id, InteractionType.RELEASE_MENU_SHOW, 0);
                    else
                        interCbWithIdleRefresh(id, InteractionType.RELEASE_MENU_CANCEL, 0);
                }
                else
                {
                    interCbWithIdleRefresh(id, InteractionType.RELEASE, hand);
                }
            }
            else if (oldState == HandState.Lasso)
            {
                interCbWithIdleRefresh(id, InteractionType.END_DRAW, hand);
            }


            //filter on current state
            if (curState == HandState.Closed)
            {
                if (uiInterState == UIInterState.PEEKING_MENU && uiInteracting)
                {
                    uiInterState = UIInterState.GRABBING_MENU;
                    interCbWithIdleRefresh(id, InteractionType.GRAB_MENU, hand);
                }
                else
                {
                    interCbWithIdleRefresh(id, InteractionType.GRAB, hand);
                }
            }
            else if (curState == HandState.Lasso)
            {
                interCbWithIdleRefresh(id, InteractionType.BEGIN_DRAW, hand);
            }
        }

        public Vector2 CameraPointToScreen(CameraSpacePoint p)
        {
            Vector3 screenPoint3 = viewport.Project(p.ToXNA(), projetion, Matrix.Identity, Matrix.Identity);
            return new Vector2(screenPoint3.X, screenPoint3.Y);
        }
    }
}
