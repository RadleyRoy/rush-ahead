using System.Threading.Tasks;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The game's speakers: one-off sounds (flat for the interface, placed in the world for what happens on the water),
    /// the river's ambience, and music that crossfades between the menu and race loops. Sound effects and music each
    /// have their own volume, set from Settings. Absent (null) in tests and tools, so everything that plays sound
    /// checks for it.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        const int Voices = 16;
        const float MusicLevel = 0.32f;
        const float AmbienceLevel = 0.22f;
        const float MusicFadeSeconds = 1.2f;

        AudioSource[] flat;
        AudioSource[] placed;
        int nextFlat;
        int nextPlaced;
        AudioSource musicA;
        AudioSource musicB;
        AudioSource ambience;
        AudioClip menuMusic;
        AudioClip raceMusic;
        Task<System.Collections.Generic.Dictionary<string, float[]>> soundsTask;
        System.Diagnostics.Stopwatch clock;
        Task<float[]> menuTask;
        Task<float[]> raceTask;
        bool wantRace;
        float musicFade = 1f;

        public static GameAudio Instance { get; private set; }

        public SoundBank Bank { get; private set; }

        /// <summary>0 to 1.</summary>
        public float EffectsVolume { get; set; } = 1f;

        public float MusicVolume { get; set; } = 0.7f;

        public static GameAudio Create()
        {
            if (Instance != null) return Instance;
            var audio = new GameObject("Audio").AddComponent<GameAudio>();
            audio.Bank = new SoundBank(); // silent until the sounds are made, a moment from now
            audio.clock = System.Diagnostics.Stopwatch.StartNew();
            audio.soundsTask = Task.Run(SoundBank.Render);
            audio.flat = new AudioSource[Voices];
            audio.placed = new AudioSource[Voices];
            for (var i = 0; i < Voices; i++)
            {
                audio.flat[i] = audio.Source(0f);
                var at = new GameObject("Voice").AddComponent<AudioSource>();
                at.transform.SetParent(audio.transform, false);
                Spatial(at);
                audio.placed[i] = at;
            }

            audio.musicA = audio.Source(0f, loop: true);
            audio.musicB = audio.Source(0f, loop: true);
            audio.ambience = audio.Source(0f, loop: true);

            // The music takes a moment to render: do it off the main thread while the title shows.
            audio.menuTask = Task.Run(() => Music.Menu());
            audio.raceTask = Task.Run(() => Music.Race());
            Instance = audio;
            return audio;
        }

        /// <summary>Makes a source sound like it's out on the water: louder close to the camera, quieter across the river.</summary>
        public static void Spatial(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 0.75f; // mostly placed, so your own raft never drops out as the camera swings
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 6f;
            source.maxDistance = 45f;
            source.dopplerLevel = 0f;
        }

        AudioSource Source(float spatial, bool loop = false)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial;
            source.loop = loop;
            return source;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>A sound for the interface or the race itself (countdown, chimes), the same wherever the camera is.</summary>
        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || EffectsVolume <= 0f) return;
            var source = flat[nextFlat];
            nextFlat = (nextFlat + 1) % flat.Length;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume * EffectsVolume);
        }

        /// <summary>A sound from somewhere on the water: a bump, a splash, a burst.</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || EffectsVolume <= 0f) return;
            var source = placed[nextPlaced];
            nextPlaced = (nextPlaced + 1) % placed.Length;
            source.transform.position = position;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume * EffectsVolume);
        }

        /// <summary>The menu loop or the race loop. Switching crossfades.</summary>
        public void SetMusic(bool racing) => wantRace = racing;

        public void SetAmbience(AudioClip clip)
        {
            if (ambience.clip == clip) return;
            ambience.clip = clip;
            if (clip != null) ambience.Play();
            else ambience.Stop();
        }

        void Update()
        {
            if (soundsTask is { IsCompletedSuccessfully: true })
            {
                Bank = SoundBank.From(soundsTask.Result);
                soundsTask = null;
                Debug.Log($"[RushAhead] Sound effects ready after {clock.ElapsedMilliseconds} ms");
            }

            if (menuMusic == null && menuTask is { IsCompletedSuccessfully: true }) menuMusic = Synth.Clip("Menu music", menuTask.Result, Music.Rate);
            if (raceMusic == null && raceTask is { IsCompletedSuccessfully: true }) raceMusic = Synth.Clip("Race music", raceTask.Result, Music.Rate);

            ambience.volume = AmbienceLevel * EffectsVolume;

            // musicA always plays what's wanted; a change swaps the sources and fades the old one out.
            var wanted = wantRace ? raceMusic : menuMusic;
            if (wanted != null && musicA.clip != wanted)
            {
                (musicA, musicB) = (musicB, musicA);
                musicA.clip = wanted;
                musicA.Play();
                musicFade = 0f;
            }

            musicFade = Mathf.Min(1f, musicFade + Time.unscaledDeltaTime / MusicFadeSeconds);
            musicA.volume = MusicLevel * MusicVolume * musicFade;
            musicB.volume = MusicLevel * MusicVolume * (1f - musicFade);
            if (musicFade >= 1f && musicB.isPlaying) musicB.Stop();
        }
    }
}
