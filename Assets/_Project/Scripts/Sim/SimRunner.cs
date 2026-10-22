using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>Drives a <see cref="SimWorld"/> from Unity's fixed-step loop at the configured tick rate.</summary>
    public sealed class SimRunner : MonoBehaviour
    {
        public SimWorld World { get; private set; }

        public void Init(SimConfig config)
        {
            World = new SimWorld(config);
            Time.fixedDeltaTime = config.TickDt;
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }

        void FixedUpdate() => World?.StepForces(Time.fixedDeltaTime);
    }
}
