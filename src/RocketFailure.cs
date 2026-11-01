using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RocketFailure
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RocketFailure : MonoBehaviour
    {
        const string Tag = "[RocketFailure] ";
        const float TickInterval = 0.25f;       // 4 Hz polling, no per-frame work
        const double DisarmHeight = 20;          // metres above ground before an armed vessel is considered "flying" again
        const int TerrainMask = (1 << 15) | (1 << 28); // Local Scenery + TerrainColliders

        // ---- settings (GameData/RocketFailure/settings.cfg) ----
        static bool modEnabled = true;
        static bool tippingEnabled = true;
        static bool splashTippingEnabled = true;
        static bool sinkingEnabled = true;
        static bool impactEnabled = true;
        static bool includeDebris = false;
        static float tiltAngle = 60f;
        static float splashTiltAngle = 75f;
        static float minVesselHeight = 2.5f;
        static float failureDelay = 3f;
        static float sinkDepth = 10f;
        static float impactSpeed = 20f;
        static float failureChance = 1f;

        class State
        {
            public bool armed;          // vessel was seen standing upright while landed/splashed
            public int airborneTicks;
            public double lastVSpeed;
            public float dangerSince = -1f;
            public bool spared;         // failure roll passed; no reroll until the condition clears
            public int seen;
        }

        readonly Dictionary<Guid, State> states = new Dictionary<Guid, State>();
        readonly List<Vessel> doomed = new List<Vessel>();
        readonly List<string> reasons = new List<string>();
        readonly List<Guid> stale = new List<Guid>();
        readonly List<Part> partBuf = new List<Part>();
        int tick;

        void Start()
        {
            LoadSettings();
            if (modEnabled)
                InvokeRepeating("Tick", 1f, TickInterval);
        }

        void Tick()
        {
            if (!FlightGlobals.ready) return;
            tick++;

            List<Vessel> loaded = FlightGlobals.VesselsLoaded;
            for (int i = 0; i < loaded.Count; i++)
            {
                Vessel v = loaded[i];
                if (v == null || v.packed || !Eligible(v)) continue;

                State s;
                if (!states.TryGetValue(v.id, out s))
                {
                    s = new State();
                    states.Add(v.id, s);
                }
                s.seen = tick;

                string reason = Evaluate(v, s);
                if (reason != null)
                {
                    doomed.Add(v);
                    reasons.Add(reason);
                }
            }

            // Destroy outside the loop: exploding parts mutates VesselsLoaded.
            for (int i = 0; i < doomed.Count; i++)
                Explode(doomed[i], reasons[i]);
            doomed.Clear();
            reasons.Clear();

            if (tick % 40 == 0) Prune();
        }

        static bool Eligible(Vessel v)
        {
            if (v.isEVA || v.state == Vessel.State.DEAD) return false;
            if (v.situation == Vessel.Situations.PRELAUNCH) return false; // never touch vessels on the pad/runway
            switch (v.vesselType)
            {
                case VesselType.Flag:
                case VesselType.SpaceObject:
                case VesselType.Unknown:
                    return false;
                case VesselType.Debris:
                    return includeDebris;
            }
            return true;
        }

        // Returns a failure reason, or null if the vessel is safe this tick.
        string Evaluate(Vessel v, State s)
        {
            bool landed = v.situation == Vessel.Situations.LANDED;
            bool splashed = v.situation == Vessel.Situations.SPLASHED;

            if (!landed && !splashed)
            {
                double ground = v.mainBody.ocean ? Math.Max(v.terrainAltitude, 0) : v.terrainAltitude;
                if (v.altitude - ground > DisarmHeight) s.armed = false; // short bounces keep the arm
                s.airborneTicks++;
                s.lastVSpeed = v.verticalSpeed;
                s.dangerSince = -1f;
                s.spared = false;
                return null;
            }

            // Touchdown: require 2+ airborne samples so load-time situation flicker can't fake an impact.
            bool touchdown = s.airborneTicks >= 2;
            s.airborneTicks = 0;
            if (touchdown && impactEnabled && -s.lastVSpeed > impactSpeed)
                return Roll(s) ? "impact at " + (-s.lastVSpeed).ToString("F1") + " m/s" : null;

            bool tipped = false;
            if (landed ? tippingEnabled : splashTippingEnabled)
            {
                Vector3 axis = v.ReferenceTransform.up;
                Vector3 normal = splashed ? (Vector3)v.upAxis : SurfaceNormal(v);
                float tilt = Vector3.Angle(axis, normal);
                float limit = splashed ? splashTiltAngle : tiltAngle;

                // Only vessels seen clearly upright after touchdown can "tip over".
                // This exempts planes, rovers, capsules floating on their side and vessels loaded already lying down.
                if (!s.armed && tilt < limit * 0.5f && Height(v, axis) >= minVesselHeight)
                    s.armed = true;
                tipped = s.armed && tilt > limit;
            }

            bool sunk = splashed && sinkingEnabled && v.altitude < -sinkDepth;

            if (!tipped && !sunk)
            {
                s.dangerSince = -1f;
                s.spared = false;
                return null;
            }
            if (s.spared) return null;

            if (s.dangerSince < 0f)
            {
                s.dangerSince = Time.time;
                if (v == FlightGlobals.ActiveVessel)
                    ScreenMessages.PostScreenMessage(sunk ? "Vessel sinking!" : "Vessel tipping over!", failureDelay, ScreenMessageStyle.UPPER_CENTER);
                return null;
            }
            if (Time.time - s.dangerSince < failureDelay) return null;

            return Roll(s) ? (sunk ? "submerged" : "tipped over") : null;
        }

        static bool Roll(State s)
        {
            if (failureChance >= 1f || UnityEngine.Random.value < failureChance) return true;
            s.spared = true;
            return false;
        }

        static Vector3 SurfaceNormal(Vessel v)
        {
            Vector3 up = v.upAxis;
            RaycastHit hit;
            if (Physics.Raycast(v.transform.position + up * 5f, -up, out hit, 500f, TerrainMask, QueryTriggerInteraction.Ignore))
                return hit.normal;
            return up;
        }

        // Extent of part centres along the vessel axis. Cheap; only called until the vessel is armed.
        static float Height(Vessel v, Vector3 axis)
        {
            List<Part> ps = v.parts;
            if (ps.Count < 2) return 0f;
            Vector3 o = v.transform.position;
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < ps.Count; i++)
            {
                float d = Vector3.Dot(ps[i].transform.position - o, axis);
                if (d < min) min = d;
                if (d > max) max = d;
            }
            return max - min;
        }

        void Explode(Vessel v, string reason)
        {
            if (v == null || v.state == Vessel.State.DEAD) return;
            states.Remove(v.id); // never handle the same vessel twice
            Debug.Log(Tag + v.vesselName + " destroyed: " + reason);
            if (v == FlightGlobals.ActiveVessel)
                ScreenMessages.PostScreenMessage(v.vesselName + " destroyed: " + reason, 5f, ScreenMessageStyle.UPPER_CENTER);

            // Copy first: explode() detaches parts and mutates v.parts. Leaves first so the tree doesn't split into new vessels.
            partBuf.AddRange(v.parts);
            for (int i = partBuf.Count - 1; i >= 0; i--)
            {
                Part p = partBuf[i];
                if (p != null && p.State != PartStates.DEAD)
                    p.explode();
            }
            partBuf.Clear();
        }

        void Prune()
        {
            foreach (KeyValuePair<Guid, State> kv in states)
                if (kv.Value.seen != tick) stale.Add(kv.Key);
            for (int i = 0; i < stale.Count; i++) states.Remove(stale[i]);
            stale.Clear();
        }

        static void LoadSettings()
        {
            // settings.cfg sits next to the Plugins folder, so a renamed install folder still works.
            string dir = Path.GetDirectoryName(Path.GetDirectoryName(typeof(RocketFailure).Assembly.Location));
            string path = Path.Combine(dir, "settings.cfg");
            ConfigNode root = File.Exists(path) ? ConfigNode.Load(path) : null;
            ConfigNode n = root != null ? root.GetNode("ROCKET_FAILURE") : null;
            if (n == null)
            {
                Debug.LogWarning(Tag + "settings not found at " + path + ", using defaults");
                return;
            }
            n.TryGetValue("enabled", ref modEnabled);
            n.TryGetValue("tippingEnabled", ref tippingEnabled);
            n.TryGetValue("splashTippingEnabled", ref splashTippingEnabled);
            n.TryGetValue("sinkingEnabled", ref sinkingEnabled);
            n.TryGetValue("impactEnabled", ref impactEnabled);
            n.TryGetValue("includeDebris", ref includeDebris);
            n.TryGetValue("tiltAngle", ref tiltAngle);
            n.TryGetValue("splashTiltAngle", ref splashTiltAngle);
            n.TryGetValue("minVesselHeight", ref minVesselHeight);
            n.TryGetValue("failureDelay", ref failureDelay);
            n.TryGetValue("sinkDepth", ref sinkDepth);
            n.TryGetValue("impactSpeed", ref impactSpeed);
            n.TryGetValue("failureChance", ref failureChance);

            tiltAngle = Mathf.Clamp(tiltAngle, 10f, 179f);
            splashTiltAngle = Mathf.Clamp(splashTiltAngle, 10f, 179f);
            failureDelay = Mathf.Max(0f, failureDelay);
            sinkDepth = Mathf.Max(0f, sinkDepth);
            impactSpeed = Mathf.Max(1f, impactSpeed);
            failureChance = Mathf.Clamp01(failureChance);
        }
    }
}
