using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>Drives a <see cref="SimWorld"/> from Unity's fixed-step loop at the configured tick rate.</summary>
    public sealed class SimRunner : MonoBehaviour
    {
        /// <summary>After each tick's forces are applied (the host broadcasts snapshots from here).</summary>
        public event System.Action<int> Ticked;

        public SimWorld World { get; private set; }

        public void Init(SimConfig config)
        {
            World = new SimWorld(config);
            Time.fixedDeltaTime = config.TickDt;
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }

        void FixedUpdate()
        {
            if (World == null) return;
            World.StepForces(Time.fixedDeltaTime);
            Ticked?.Invoke(World.Tick);
        }
    }
}
