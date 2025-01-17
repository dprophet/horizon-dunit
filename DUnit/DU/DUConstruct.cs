﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Linq;
using MoonSharp.Interpreter;

namespace DUnit.DU
{
    public class DUConstruct : Elements.Element
    {
        public Universe Universe { get; private set; }

        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 Acceleration { get; private set; }

        public Vector3 Rotation { get; private set; }
        public Vector3 AngularVelocity { get; private set; }
        public Vector3 AngularAcceleration { get; private set; }


        public Vector3 AirResistance { get; private set; }
        public bool IsColliding { get; private set; }
        public new float Mass { get; private set; }
        public float CrossSectionalArea { get; private set; }
        
        public Vector3 MaxKinematicsAtmoPos { get; private set; }
        public Vector3 MaxKinematicsSpacePos { get; private set; }
        public Vector3 MaxKinematicsAtmoNeg { get; private set; }
        public Vector3 MaxKinematicsSpaceNeg { get; private set; }

        public List<Elements.Element> Elements { get; private set; }

        public DUConstruct(Universe Universe, Vector3 position, Vector3 rotation)
            :base(1, "CoreUnitDynamic")
        {
            this.Universe = Universe;
            this.Position = position;
            this.Rotation = rotation;
            this.Velocity = new Vector3(0, 0, 0);
            this.Mass = 10000;

            this.MaxKinematicsAtmoPos = new Vector3(100000, 100000, 100000);
            this.MaxKinematicsSpacePos = new Vector3(100000, 100000, 100000);
            this.MaxKinematicsAtmoNeg = new Vector3(100000, 100000, 100000);
            this.MaxKinematicsSpaceNeg = new Vector3(100000, 100000, 100000);

            this.CrossSectionalArea = 10;

            this.Elements = new List<Elements.Element>();
            this.Elements.Add(this);
        }

        public void AddElement(Elements.Element e)
        {
            this.Elements.Add(e);
        }

        public void Tick(float seconds)
        {
            

            //Calculate air resistance
            var airDensity = Universe.GetAirDensityAtPosition(Position);
            var airResistance = ((airDensity * CrossSectionalArea) / 2) * Velocity.LengthSquared();
            AirResistance = Vector3.Normalize(Velocity) * (float)(airResistance / Mass);
            if (float.IsNaN(AirResistance.X)) AirResistance = Vector3.Zero;

            var actualAcceleration = GetMaxPossibleAcceleration(Acceleration).Min(Acceleration);
            var appliedAcceleration = actualAcceleration;
            appliedAcceleration -= Universe.CalculateGravityAtPosition(Position);
            appliedAcceleration -= AirResistance;

            Velocity += (appliedAcceleration * seconds);
            var provisionalPosition = Position + (Velocity * seconds);

            if (Universe.IsCollidingWithObject(provisionalPosition))
            {
                //Tonk
                //We dont bounce `round `ere
                Velocity = Vector3.Zero;
                IsColliding = true;
            }
            else
            {
                Position = provisionalPosition;
                IsColliding = false;
            }

            //Rotation
            if (!IsColliding)
            {
                Rotation += AngularVelocity * seconds;
                AngularVelocity += AngularAcceleration * seconds;
            }
            else
            {
                Rotation += Vector3.Zero;
                AngularVelocity += Vector3.Zero;
            }
            
        }

        public void SetThrust(Vector3 accel)
        {
            this.Acceleration = accel;
        }
        public void SetRotation(Vector3 rot)
        {
            this.AngularAcceleration = rot;
        }

        public Vector4 GetAxisKinematics(Vector3 axis)
        {
            var atmoPos = (MaxKinematicsAtmoPos * axis).Length();
            var atmoNeg = (MaxKinematicsAtmoNeg * axis).Length();
            var spacePos = (MaxKinematicsSpacePos * axis).Length();
            var spaceNeg = (MaxKinematicsSpaceNeg * axis).Length();

            return new Vector4(atmoPos, atmoNeg, spacePos, spaceNeg);
        }

        public Vector3 GetMaxPossibleAcceleration(Vector3 direction)
        {
            Vector3 currentPos = Vector3.Zero;
            Vector3 currentNeg = Vector3.Zero;

            if (Universe.GetAirDensityAtPosition(Position) > 0)
            {
                currentNeg = MaxKinematicsAtmoNeg;
                currentPos = MaxKinematicsAtmoPos;
            }
            else
            {
                currentNeg = MaxKinematicsSpaceNeg;
                currentPos = MaxKinematicsSpacePos;
            }

            return new Vector3(
                direction.X >= 0 ? currentPos.X / Mass : currentNeg.X / Mass,
                direction.Y >= 0 ? currentPos.Y / Mass : currentNeg.Y / Mass,
                direction.Z >= 0 ? currentPos.Z / Mass : currentNeg.Z / Mass
                );
        }

        public override Table GetTable(Script lua)
        {
            var construct = base.GetTable(lua);

            construct["getMass"] = new Func<float>(() => Mass);
            construct["getIMass"] = new Func<float>(() => Mass * (float)(1 / Math.Sqrt(1 - Velocity.LengthSquared() / Math.Pow(Universe.C, 2))));
            construct["getWorldPosition"] = new Func<float[]>(() => Position.ToLua());
            construct["setWorldPosition"] = new Func<float[], bool>((P) => { Position = new Vector3(P[0], P[1], P[2]); return true; });
            construct["getCrossSection"] = new Func<float>(() => CrossSectionalArea);
            construct["getWorldVelocity"] = new Func<float[]>(() => Velocity.ToLua());
            construct["setWorldVelocity"] = new Func<float[], bool>((V) => { Velocity = new Vector3(V[0], V[1], V[2]); return true; });
            construct["getWorldAcceleration"] = new Func<float[]>(() => Acceleration.ToLua());

            construct["getWorldOrientationUp"] = new Func<float[]>(() => (Rotation * Vector3.UnitY).ToLua());
            construct["getWorldOrientationRight"] = new Func<float[]>(() => (Rotation * Vector3.UnitX).ToLua());
            construct["getWorldOrientationForward"] = new Func<float[]>(() => (Rotation * Vector3.UnitZ).ToLua());

            construct["getWorldGravity"] = new Func<float[]>(() => Universe.CalculateGravityAtPosition(Position).ToLua());
            construct["getGravityIntensity"] = new Func<float>(() => Universe.CalculateGravityAtPosition(Position).Length());
            construct["getWorldVertical"] = new Func<float[]>(() => Universe.CalculateGravityAtPosition(Position).ToLua());
            construct["getWorldAirFrictionAcceleration"] = new Func<float[]>(() => AirResistance.ToLua());

            construct["getWorldAngularVelocity"] = new Func<float[]>(() => AngularVelocity.ToLua());
            construct["getWorldAngularAcceleration"] = new Func<float[]>(() => AngularAcceleration.ToLua());
            construct["getWorldAirFrictionAngularAcceleration"] = new Func<float[]>(() => Vector3.Zero.ToLua()); //No idea how to simulate this

            construct["getAltitude"] = new Func<float>(() => (float)Universe.GetAltitude(Position));
            construct["getId"] = new Func<int>(() => 1);

            construct["getVelocity"] = new Func<float[]>(() => Vector3.Zero.ToLua());//Im lazy
            construct["getAcceleration"] = new Func<float[]>(() => Vector3.Zero.ToLua());//Im lazy

            construct["getMaxThrustAlongAxis"] = new Func<string, float[], float[]>((T, D) => GetAxisKinematics(new Vector3(D[0], D[1], D[2])).ToLua());

            construct["spawnNumberSticker"] = new Func<int, float, float, float, string, int>((nb, x, y, z, orientation) => -1);
            construct["spawnArrowSticker"] = new Func<float, float, float, string, bool>((x, y, z, orientation) => true);
            construct["deleteSticker"] = new Func<int, bool>((index) => true);
            construct["moveSticker"] = new Func<int, float, float, float, bool>((index, x, y, z) => true);
            construct["rotateSticker"] = new Func<int, float, float, float, bool>((index, angle_x, angle_y, angle_z) => true);

            construct["getElementIdList"] = new Func<int[]>(() => this.Elements.Select(x => x.ID).ToArray().ToArray());
            construct["getElementTypeById"] = new Func<int, string>((uid) => this.Elements.Where(x => x.ID == uid).FirstOrDefault()?.ClassName ?? null);
            construct["getElementHitPointsById"] = new Func<int, int>((uid) => this.Elements.Where(x => x.ID == uid).FirstOrDefault()?.HitPoints ?? 0);
            construct["getElementMaxHitPointsById"] = new Func<int, int>((uid) => this.Elements.Where(x => x.ID == uid).FirstOrDefault()?.MaxHitPoints ?? 0);
            construct["getElementMassById"] = new Func<int, float>((uid) => this.Elements.Where(x => x.ID == uid).FirstOrDefault()?.Mass ?? 0);
            construct["getElementPositionById"] = new Func<int, float[]>((uid) => Vector3.Zero.ToLua());
            construct["getElementRotationById"] = new Func<int, float[]>((uid) => Vector3.Zero.ToLua());
            construct["getElementTagsById"] = new Func<int, string>((uid) => "");



            return construct;
        }
    }
}
