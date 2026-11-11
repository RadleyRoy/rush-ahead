using System;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Old Snapjaw, drawn from its state (the host's boss, or a joined player's snapshots, so everyone sees the same):
    /// the crocodile sculpture three times over in darker, mossier hide, rising with a roar and a splash, its jaws
    /// working as it swims, its eyes burning red and a red ring on the water when it's about to lunge, jaws wide while it
    /// surges, and a crunch when it bites. It sinks out of sight when it dives.
    /// </summary>
    public sealed class BossView : MonoBehaviour
    {
        const float Sunk = -2.6f;

        ArtPalette palette;
        Transform body;
        Transform jaw;
        Renderer[] eyes;
        Material eyeCalm;
        Material eyeAngry;
        Transform ring;
        Material ringMaterial;
        float height = Sunk;
        Transform wake;
        bool shown;
        BossMood mood;
        int chomps = -1;

        /// <summary>Where it is and what it's doing now.</summary>
        public Func<(bool present, Vector2 position, float angleRad, BossMood mood, int chomps)> State { get; set; }

        public static BossView Build(ArtPalette palette, Transform parent)
        {
            var view = new GameObject("Old Snapjaw").AddComponent<BossView>();
            view.transform.SetParent(parent, false);
            view.palette = palette;
            view.BuildModel();
            view.body.gameObject.SetActive(false);
            return view;
        }

        void BuildModel()
        {
            body = Shapes3D.Group(transform, "Body");
            var scale = BossCroc.Length / Hazards.CrocLength;
            var model = Shapes3D.Group(body, "Model");
            model.localScale = Vector3.one * scale;
            eyeCalm = palette.Glow(new Color(1f, 0.85f, 0.3f));
            eyeAngry = palette.Glow(new Color(1f, 0.12f, 0.08f));
            if (palette.HasCreature(Creature.CrocBody) && palette.HasCreature(Creature.CrocJaw))
            {
                var hide = palette.Hide(new Color(0.2f, 0.26f, 0.14f), new Color(0.55f, 0.56f, 0.38f), new Color(0.08f, 0.1f, 0.06f), new Color(0.93f, 0.9f, 0.8f));
                Shapes3D.MeshObject("Body", palette.CreatureMesh(Creature.CrocBody), model, Vector3.zero, hide);
                jaw = Shapes3D.Group(model, "Upper jaw", new Vector3(0.8f, 0.18f, 0f));
                Shapes3D.MeshObject("Snout", palette.CreatureMesh(Creature.CrocJaw), jaw, Vector3.zero, hide);
            }
            else
            {
                var skin = palette.Tinted(palette.animal, new Color(0.2f, 0.3f, 0.15f));
                var length = Hazards.CrocLength;
                Shapes3D.Primitive(PrimitiveType.Capsule, model, new Vector3(-0.1f, 0.12f, 0f), new Vector3(0.75f, length * 0.32f, 0.6f), skin, Quaternion.Euler(0f, 0f, 90f), "Body");
                jaw = Shapes3D.Group(model, "Upper jaw", new Vector3(length * 0.2f, 0.2f, 0f));
                Shapes3D.Primitive(PrimitiveType.Cube, jaw, new Vector3(0.45f, 0f, 0f), new Vector3(0.85f, 0.16f, 0.44f), skin, name: "Snout");
            }

            eyes = new Renderer[2];
            for (var i = 0; i < 2; i++)
                eyes[i] = Shapes3D.Primitive(PrimitiveType.Sphere, model, new Vector3(0.64f, 0.34f, i == 0 ? 0.13f : -0.13f), Vector3.one * 0.12f, eyeCalm, name: "Eye")
                    .GetComponent<Renderer>();

            // Moss trailing off its back: it's been down there a long time.
            var moss = palette.Tinted(palette.grass, new Color(0.3f, 0.4f, 0.18f));
            for (var i = 0; i < 4; i++)
                Shapes3D.Primitive(PrimitiveType.Cube, model, new Vector3(-0.5f + i * 0.3f, 0.36f, 0f), new Vector3(0.18f, 0.05f, 0.3f), moss, name: "Moss");

            wake = Shapes3D.MeshObject("Wake", Shapes3D.Disc(BossCroc.Length * 0.55f), transform, Vector3.zero, palette.foam).transform;
            ringMaterial = new Material(palette.foam) { name = "Lunge ring", color = new Color(1f, 0.15f, 0.1f, 0.8f) };
            ring = Shapes3D.MeshObject("Lunge ring", Shapes3D.Torus(1f, 0.08f, 48, 6), transform, Vector3.zero, ringMaterial).transform;
            ring.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var state = State?.Invoke() ?? default;
            var audio = GameAudio.Instance;
            var newMood = state.present ? state.mood : BossMood.Hidden;
            var position = SimSpace.ToWorld(state.position);
            if (newMood != mood)
            {
                if (audio != null && audio.Bank != null)
                {
                    if (newMood == BossMood.Rising) audio.PlayAt(audio.Bank.Roar, position, 1f, 0.85f);
                    if (newMood == BossMood.Lunging) audio.PlayAt(audio.Bank.Roar, position, 1f, 1.15f);
                }

                if (newMood is BossMood.Rising or BossMood.Diving) Splashes.Splash(position + Vector3.up * 0.3f, 2.2f, palette);
                mood = newMood;
            }

            if (chomps >= 0 && state.chomps != chomps && audio != null && audio.Bank != null) audio.PlayAt(audio.Bank.Chomp, position, 1f);
            chomps = state.chomps;

            var up = mood is BossMood.Chasing or BossMood.Lunging or BossMood.Surging || (mood == BossMood.Rising);
            if (!state.present)
            {
                height = Sunk;
                shown = false;
                body.gameObject.SetActive(false);
                ring.gameObject.SetActive(false);
                wake.gameObject.SetActive(false);
                return;
            }

            if (!shown)
            {
                shown = true;
                height = Sunk;
                body.gameObject.SetActive(true);
            }

            height = Mathf.MoveTowards(height, up ? 0f : Sunk, Time.deltaTime * (up ? 2.6f : 2.2f));
            var t = Time.time;
            var heading = Quaternion.Euler(0f, -state.angleRad * Mathf.Rad2Deg, 0f); // the sculpture's snout points along +x
            var sway = Quaternion.Euler(0f, 6f * Mathf.Sin(t * 3.1f), 0f);
            body.SetPositionAndRotation(position + Vector3.up * (height + 0.05f * Mathf.Sin(t * 2f)), heading * sway);
            wake.gameObject.SetActive(height > Sunk * 0.6f);
            wake.position = position + Vector3.up * 0.03f;

            var open = mood switch
            {
                BossMood.Surging => 38f,
                BossMood.Lunging => 14f + 8f * Mathf.Sin(t * 18f),
                _ => 6f + 6f * Mathf.Max(0f, Mathf.Sin(t * 2.2f)),
            };
            if (jaw != null) jaw.localRotation = Quaternion.Euler(0f, 0f, open);

            var angry = mood is BossMood.Lunging or BossMood.Surging;
            foreach (var eye in eyes) eye.sharedMaterial = angry ? eyeAngry : eyeCalm;

            // The tell: a red ring on the water where its jaws are about to be.
            ring.gameObject.SetActive(mood == BossMood.Lunging);
            if (mood == BossMood.Lunging)
            {
                var forward = new Vector2(Mathf.Cos(state.angleRad), Mathf.Sin(state.angleRad));
                ring.position = SimSpace.ToWorld(state.position + forward * (BossCroc.JawReach + 4f), 0.06f);
                ring.localScale = Vector3.one * (BossCroc.BiteRadius + 1.4f) * (1f + 0.12f * Mathf.Sin(t * 14f));
            }
        }
    }
}
