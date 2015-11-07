﻿using System;
using System.Collections.Generic;
using System.Linq;
using Hedgehog.Core.Actors;
using Hedgehog.Core.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Hedgehog.Core.Triggers
{
    /// <summary>
    /// An event for when a controller lands on a platform, invoked with the offending controller and
    /// the offending platform.
    /// </summary>
    [Serializable]
    public class PlatformSurfaceEvent : UnityEvent<TerrainCastHit> { }

    /// <summary>
    /// An event for when a controller collides with a platform, invoked with the offending controller
    /// and the offending platform.
    /// </summary>
    [Serializable]
    public class PlatformCollisionEvent : UnityEvent<TerrainCastHit> { }

    /// <summary>
    /// Hook up to these events to react when a controller lands on the object.
    /// </summary>
    [AddComponentMenu("Hedgehog/Triggers/Platform Trigger")]
    public class PlatformTrigger : BaseTrigger
    {
        /// <summary>
        /// Whether to always collide regardless of a controller's collision flags.
        /// </summary>
        [SerializeField]
        [Tooltip("Whether to always collide regardless of a controller's collision flags.")]
        public bool IgnoreLayers;

        /// <summary>
        /// Called when a controller lands on the surface of the platform.
        /// </summary>
        [SerializeField] public PlatformSurfaceEvent OnSurfaceEnter;

        /// <summary>
        /// Called while a controller is on the surface of the platform.
        /// </summary>
        [SerializeField] public PlatformSurfaceEvent OnSurfaceStay;

        /// <summary>
        /// Called when a controller exits the surface of the platform.
        /// </summary>
        [SerializeField] public PlatformSurfaceEvent OnSurfaceExit;

        /// <summary>
        /// Called when a controller begins colliding with the platform.
        /// </summary>
        [SerializeField] public PlatformCollisionEvent OnPlatformEnter;

        /// <summary>
        /// Called while a controller is colliding with the platform.
        /// </summary>
        [SerializeField] public PlatformCollisionEvent OnPlatformStay;

        /// <summary>
        /// Called when a controller stops colliding with the platform.
        /// </summary>
        [SerializeField] public PlatformCollisionEvent OnPlatformExit;

        /// <summary>
        /// Returns whether the platform should be collided with based on the result of the specified
        /// terrain cast.
        /// </summary>
        /// <param name="hit">The specified terrain cast.</param>
        /// <returns></returns>
        public delegate bool CollisionPredicate(TerrainCastHit hit);

        /// <summary>
        /// A list of predicates which, if empty or all return true, allow collision with the
        /// platform.
        /// </summary>
        public ICollection<CollisionPredicate> CollisionRules;

        /// <summary>
        /// Returns whether the controller is considered to be on the surface.
        /// </summary>
        /// <returns></returns>
        public delegate bool SurfacePredicate(HedgehogController controller, TerrainCastHit hit);

        /// <summary>
        /// A list of predicates which invokes surface events based on whether it is empty or all
        /// return true.
        /// </summary>
        public ICollection<SurfacePredicate> SurfaceRules;

        /// <summary>
        /// A list of current collisions;
        /// </summary>
        public List<TerrainCastHit> Collisions;

        private List<TerrainCastHit> _notifiedCollisions;

        /// <summary>
        /// A list of current surface collisions.
        /// </summary>
        public List<TerrainCastHit> SurfaceCollisions;

        private List<TerrainCastHit> _notifiedSurfaceCollisions;

        public override void Reset()
        {
            base.Reset();

            IgnoreLayers = false;

            OnPlatformEnter = new PlatformCollisionEvent();
            OnPlatformStay = new PlatformCollisionEvent();
            OnPlatformExit = new PlatformCollisionEvent();

            OnSurfaceEnter = new PlatformSurfaceEvent();
            OnSurfaceStay = new PlatformSurfaceEvent();
            OnSurfaceExit = new PlatformSurfaceEvent();
        }

        public override void Awake()
        {
            base.Awake();

            OnPlatformEnter = OnPlatformEnter ?? new PlatformCollisionEvent();
            OnPlatformStay = OnPlatformStay ?? new PlatformCollisionEvent();
            OnPlatformExit = OnPlatformExit ?? new PlatformCollisionEvent();

            OnSurfaceEnter = OnSurfaceEnter ?? new PlatformSurfaceEvent();
            OnSurfaceStay = OnSurfaceStay ?? new PlatformSurfaceEvent();
            OnSurfaceExit = OnSurfaceExit ?? new PlatformSurfaceEvent();

            CollisionRules = new List<CollisionPredicate>();
            Collisions = new List<TerrainCastHit>();
            _notifiedCollisions = new List<TerrainCastHit>();

            SurfaceRules = new List<SurfacePredicate>();
            SurfaceCollisions = new List<TerrainCastHit>();
            _notifiedSurfaceCollisions = new List<TerrainCastHit>();
        }

        public virtual void Start()
        {
            var collider2D = GetComponent<Collider2D>();
            if (collider2D != null) collider2D.isTrigger = false;
        }

        public virtual void FixedUpdate()
        {
            if (!(Collisions.Any() || _notifiedCollisions.Any() || SurfaceCollisions.Any() ||
                _notifiedSurfaceCollisions.Any()))
                return;

            // Remove collisions that were recorded last update but not this one. Invoke their "exit" events.
            Collisions.RemoveAll(CollisionsRemover);

            // Move this update's collision list to the last update's.
            Collisions = _notifiedCollisions;

            // Invoke their "stay" events if they still fulfill CollidesWith.
            foreach (var collision in new List<TerrainCastHit>(Collisions))
                if(CollidesWith(collision)) OnPlatformStay.Invoke(collision);

            // Make room in the collision list for the next update.
            _notifiedCollisions = new List<TerrainCastHit>();

            // Remove surface collisions that were recorded last update but not this one. Invoke their "exit" events.
            SurfaceCollisions.RemoveAll(SurfaceCollisionsRemover);

            // Move this update's surface collision list to the last update's.
            SurfaceCollisions = _notifiedSurfaceCollisions;

            // Invoke their "stay" events if they still fulfill IsOnSurface.
            foreach (var collision in new List<TerrainCastHit>(SurfaceCollisions))
                if(IsOnSurface(collision.Controller, collision)) OnSurfaceStay.Invoke(collision);

            // Make room in the surface collision list for the next update.
            _notifiedSurfaceCollisions = new List<TerrainCastHit>();
        }

        public override bool HasController(HedgehogController controller)
        {
            return Collisions.Any(hit => hit.Controller == controller);
        }
        #region Collision List Removers
        private bool CollisionsRemover(TerrainCastHit hit)
        {
            if (_notifiedCollisions.Any(castHit => castHit.Controller == hit.Controller))
                return false;

            OnPlatformExit.Invoke( hit);
            return true;
        }

        private bool SurfaceCollisionsRemover(TerrainCastHit hit)
        {
            if (_notifiedSurfaceCollisions.Any(castHit => castHit.Controller == hit.Controller))
                return false;
            
            OnSurfaceExit.Invoke(hit);
            return true;
        }
        #endregion
        #region Notify Functions
        /// <summary>
        /// Lets the trigger know about a collision with a controller.
        /// </summary>
        /// <param name="controller">The specified controller.</param>
        /// <param name="hit">The collision data.</param>
        public void NotifyCollision(HedgehogController controller, TerrainCastHit hit)
        {
            if (!CollidesWith(hit))
                return;
            
            if (Collisions.All(castHit => castHit.Controller != controller))
            {
                Collisions.Add(hit);
                _notifiedCollisions.Add(hit);
                
                OnPlatformEnter.Invoke(hit);
            }
            else if (_notifiedCollisions.All(castHit => castHit.Controller != controller))
            {
                _notifiedCollisions.Add(hit);
            }
        }

        /// <summary>
        /// Lets the trigger know about a controller standing on its surface.
        /// </summary>
        /// <param name="controller">The specified controller.</param>
        /// <param name="hit">The collision data.</param>
        public void NotifySurfaceCollision(HedgehogController controller, TerrainCastHit hit)
        {
            if (!IsOnSurface(controller, hit))
                return;

            if (SurfaceCollisions.Any(castHit => castHit.Controller == controller))
            {
                if (_notifiedSurfaceCollisions.All(castHit => castHit.Controller != controller))
                    _notifiedSurfaceCollisions.Add(hit);
            }
            else
            {
                SurfaceCollisions.Add(hit);
                
                if (_notifiedSurfaceCollisions.All(castHit => castHit.Controller != controller))
                    _notifiedSurfaceCollisions.Add(hit);

                OnSurfaceEnter.Invoke(hit);
            }
        }

        #endregion
        #region Collision Rules
        public bool IsOnSurface(HedgehogController controller, TerrainCastHit hit)
        {
            if (!SurfaceRules.Any()) return DefaultSurfaceRule(controller, hit);
            return SurfaceRules.All(predicate => predicate(controller, hit));
        }

        /// <summary>
        /// Returns whether the platform can be collided with based on its list of collision predicates
        /// and the specified results of a terrain cast.
        /// </summary>
        /// <param name="hit">The specified results of a terrain cast.</param>
        /// <returns></returns>
        public bool CollidesWith(TerrainCastHit hit)
        {
            if (!CollisionRules.Any()) return DefaultCollisionRule(hit);
            return CollisionRules.All(predicate => predicate(hit));
        }

        public bool DefaultSurfaceRule(HedgehogController controller, TerrainCastHit hit)
        {
            if (!controller.Grounded) return false;
            return hit.Hit.transform == controller.PrimarySurface || hit.Hit.transform == controller.SecondarySurface;
        }

        public bool DefaultCollisionRule(TerrainCastHit hit)
        {
            return IgnoreLayers || TerrainUtility.CollisionModeSelector(hit.Hit.transform, hit.Controller);
        }
        #endregion
    }
}
