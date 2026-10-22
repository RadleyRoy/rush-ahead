using UnityEngine;

namespace RushAhead.Content
{
    public enum AiSkill
    {
        Clumsy,
        Keen,
        Ace,

        /// <summary>No personality at all: a steady, flawless crew for tests and the autopilot demo.</summary>
        Steady,
    }

    /// <summary>
    /// How an AI crewmate behaves. Imperfection is the point: a perfect crew is boring and makes the players feel
    /// irrelevant, so even the best one reacts late, wobbles its aim and gets the air wrong now and then.
    /// The default, Keen, is enthusiastic and slightly wrong.
    /// </summary>
    [CreateAssetMenu(menuName = "Rush Ahead/AI Profile", fileName = "AiProfile")]
    public sealed class AiProfile : ScriptableObject
    {
        public string displayName = "Keen";
        [TextArea] public string blurb = "";

        [Tooltip("The speed it tries to keep the raft at, in metres per second.")]
        [Range(1f, 5f)] public float cruiseSpeed = 3f;

        [Tooltip("How long it takes to act on what it sees, or on an order.")]
        [Range(0f, 1f)] public float reactionSeconds = 0.3f;

        [Tooltip("How far its aim wanders either side of where it means to blow.")]
        [Range(0f, 40f)] public float aimWobbleDeg = 10f;

        [Tooltip("0: blows whenever it can and runs dry at the worst moment. 1: takes turns and only blows when it helps.")]
        [Range(0f, 1f)] public float airDiscipline = 0.6f;

        [Tooltip("How readily it braces when the raft leans hard.")]
        [Range(0f, 1f)] public float braceInstinct = 0.5f;

        [Tooltip("How far over the raft must lean (0 level, 1 capsizing) before it reacts.")]
        [Range(0.2f, 1f)] public float panicLean = 0.7f;

        [Tooltip("Whether it thinks to use a burst when the raft is stuck.")]
        [Range(0f, 1f)] public float burstWhenStuck = 0.5f;

        [Tooltip("How often, per second, it drifts off and stops blowing for a couple of seconds.")]
        [Range(0f, 0.5f)] public float daydream = 0.05f;

        /// <summary>The built-in tunings. Assets made from these can be retuned in the Inspector.</summary>
        public static AiProfile Preset(AiSkill skill)
        {
            var p = CreateInstance<AiProfile>();
            p.name = skill.ToString();
            p.displayName = skill.ToString();
            switch (skill)
            {
                case AiSkill.Clumsy:
                    p.blurb = "Means well. Slow to react, wobbly aim, and easily distracted.";
                    p.cruiseSpeed = 2f; p.reactionSeconds = 0.6f; p.aimWobbleDeg = 30f;
                    p.airDiscipline = 0.15f; p.braceInstinct = 0.25f; p.panicLean = 0.85f; p.burstWhenStuck = 0.2f; p.daydream = 0.25f;
                    break;
                case AiSkill.Keen:
                    p.blurb = "Enthusiastic and slightly wrong. A good crew to learn with.";
                    p.cruiseSpeed = 2.9f; p.reactionSeconds = 0.3f; p.aimWobbleDeg = 12f;
                    p.airDiscipline = 0.55f; p.braceInstinct = 0.55f; p.panicLean = 0.7f; p.burstWhenStuck = 0.5f; p.daydream = 0.06f;
                    break;
                case AiSkill.Ace:
                    p.blurb = "Sharp, takes turns with the air, braces in time.";
                    p.cruiseSpeed = 3.4f; p.reactionSeconds = 0.15f; p.aimWobbleDeg = 4f;
                    p.airDiscipline = 0.9f; p.braceInstinct = 0.85f; p.panicLean = 0.6f; p.burstWhenStuck = 0.9f; p.daydream = 0f;
                    break;
                case AiSkill.Steady:
                    p.blurb = "No personality: the course-testing autopilot.";
                    p.cruiseSpeed = 3.2f; p.reactionSeconds = 0f; p.aimWobbleDeg = 0f;
                    p.airDiscipline = 1f; p.braceInstinct = 0f; p.panicLean = 1f; p.burstWhenStuck = 0f; p.daydream = 0f;
                    break;
            }

            return p;
        }
    }
}
