using System.Collections;
using System.Collections.Generic;
using RushAhead.Net;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>How far through the photo finish this device is.</summary>
    public enum PhotoFinishStage
    {
        None,
        /// <summary>The last moments before the winner crossed, in slow motion from beside the line.</summary>
        Playing,
        /// <summary>Frozen on the winner's crossing: a flash, then an old sepia photo with the margin.</summary>
        Frozen,
    }

    /// <summary>
    /// The photo finish: every device keeps the last few seconds of each raft as it drew it. When a Team Race's first
    /// two rafts cross within <see cref="PhotoFinishRule.Within"/> seconds of each other (or an Elimination's runner-up
    /// was that close when the winner crossed), it plays the run-in again at a fifth of the speed from a low camera
    /// beside the line, with the obstacles posed at the replay's clock, freezes on the winner's crossing with a flash
    /// and a sepia "photo", and keeps that picture for the results. Purely local: nothing new goes over the network.
    /// </summary>
    public sealed partial class GameController
    {
        const float PhotoRunIn = 1.6f; // seconds of race replayed before the winner's crossing
        const float PhotoSpeed = 0.2f;
        const float PhotoHold = 2.6f; // seconds frozen on the photo

        readonly List<RaftRecording> recordings = new();
        readonly List<ReplayBoat> replays = new();
        readonly List<bool> stillIn = new();
        (int venue, int start) photoRun = (-1, -1);
        bool photoDecided;
        double photoClock;
        double photoTo;
        float photoFrozenAt;
        float savedFieldOfView;
        bool savedPost;
        int photoWinner;
        int photoRunnerUp;
        PhotoFinishLook photoLook;

        public PhotoFinishStage PhotoStage { get; private set; }

        /// <summary>The frozen photo, for the results; null when this run had none.</summary>
        public Texture2D PhotoPicture { get; private set; }

        /// <summary>"BLUE BY 0.08 S": who won and by how much.</summary>
        public string PhotoCaption { get; private set; }

        /// <summary>The camera flash as the shutter goes, from 1 fading to 0.</summary>
        public float PhotoFlash => PhotoStage == PhotoFinishStage.Frozen ? Mathf.Clamp01(1f - (Time.unscaledTime - photoFrozenAt) / 0.35f) : 0f;

        /// <summary>Whether the replay is on screen (the results wait for it).</summary>
        public bool PhotoFinishPlaying => PhotoStage != PhotoFinishStage.None;

        /// <summary>Tap to skip: on to the frozen photo, or past it to the results.</summary>
        public void SkipPhotoFinish()
        {
            if (PhotoStage == PhotoFinishStage.Playing) Freeze();
            else if (PhotoStage == PhotoFinishStage.Frozen && Time.unscaledTime - photoFrozenAt > 0.5f) EndPhotoFinish();
        }

        void UpdatePhotoFinish()
        {
            if (venue == 0 || course == null || Phase == RoomPhase.Lobby)
            {
                if (PhotoStage != PhotoFinishStage.None) EndPhotoFinish();
                return;
            }

            var start = Mode == NetMode.Joined ? clientRaceStart : StartTick;
            if (photoRun != (venue, start))
            {
                // A new race: forget the last one's recordings and photo.
                if (PhotoStage != PhotoFinishStage.None) EndPhotoFinish();
                photoRun = (venue, start);
                photoDecided = false;
                stillIn.Clear();
                foreach (var recording in recordings) recording.Clear();
                if (PhotoPicture != null) Destroy(PhotoPicture);
                PhotoPicture = null;
                PhotoCaption = null;
            }

            if (PhotoStage != PhotoFinishStage.None)
            {
                StepPhotoFinish();
                return;
            }

            if (Phase != RoomPhase.Racing) return;
            var clock = ClockSeconds();
            while (recordings.Count < Rafts) recordings.Add(new RaftRecording());
            for (var raft = 0; raft < Rafts; raft++)
                if (RaftState(raft) is { } state)
                    recordings[raft].Record(state, clock);
            if (!photoDecided) DecidePhotoFinish(clock);
        }

        void DecidePhotoFinish(double clock)
        {
            if (Rafts < 2 || RaceMode == GameMode.Coop)
            {
                photoDecided = true;
                return;
            }

            // Who crossed first, and second (a Team Race).
            int first = -1, second = -1;
            for (var raft = 0; raft < Rafts; raft++)
            {
                var tick = RaftState(raft)?.FinishTick ?? 0;
                if (tick <= 0) continue;
                if (first < 0 || tick < RaftState(first).FinishTick)
                {
                    second = first;
                    first = raft;
                }
                else if (second < 0 || tick < RaftState(second).FinishTick)
                {
                    second = raft;
                }
            }

            if (first < 0)
            {
                // Nobody's home yet: note who's still in, for an Elimination's final.
                while (stillIn.Count < Rafts) stillIn.Add(false);
                for (var raft = 0; raft < Rafts; raft++) stillIn[raft] = RaftState(raft) is { Out: false };
                return;
            }

            var crossed = RaftState(first).FinishTick / (double)config.tickRate;
            float? margin = null;
            if (RaceMode == GameMode.Elimination)
            {
                photoDecided = true;
                var finish = courses[venue - 1].gates[^1];
                var closest = float.MaxValue;
                for (var raft = 0; raft < Rafts && raft < stillIn.Count; raft++)
                {
                    if (raft == first || !stillIn[raft] || recordings[raft].Count < 2) continue;
                    var (a, b, t) = recordings[raft].At(crossed);
                    var at = course.River.Project(Vector2.Lerp(a.Position, b.Position, t));
                    var speed = Vector2.Dot(Vector2.Lerp(a.Velocity, b.Velocity, t), course.River.At(at.Distance).Tangent);
                    var gap = PhotoFinishRule.Margin(finish - at.Distance, speed);
                    if (gap is not { } close || close >= closest) continue;
                    closest = close;
                    margin = close;
                    second = raft;
                }
            }
            else if (second >= 0)
            {
                photoDecided = true;
                margin = PhotoFinishRule.Margin(RaftState(first).FinishTick, RaftState(second).FinishTick, config.tickRate);
            }
            else if (clock - crossed > PhotoFinishRule.Within + 0.1)
            {
                photoDecided = true; // nobody close behind
            }

            if (margin is { } m && second >= 0 && first < recordings.Count && recordings[first].Count >= 2) PlayPhotoFinish(first, second, crossed, m);
        }

        void PlayPhotoFinish(int winner, int runnerUp, double crossed, float margin)
        {
            photoWinner = winner;
            photoRunnerUp = runnerUp;
            photoTo = crossed;
            photoClock = Mathf.Max((float)(crossed - PhotoRunIn), (float)recordings[winner][0].Clock);
            PhotoCaption = $"{TeamName(winner).ToUpperInvariant()} BY {margin:0.00} S";
            replays.Clear();
            for (var raft = 0; raft < Rafts; raft++)
            {
                var live = RaftState(raft);
                var view = ViewOf(raft);
                if (live == null || view == null || raft >= recordings.Count)
                {
                    replays.Add(null);
                    continue;
                }

                var replay = new ReplayBoat(live, recordings[raft]);
                replay.Show(photoClock);
                replays.Add(replay);
                view.Source = replay;
            }

            var camera = Camera.main;
            savedFieldOfView = camera != null ? camera.fieldOfView : 60f;
            GameUi.CameraHeld = true;
            PhotoStage = PhotoFinishStage.Playing;
            var audio = GameAudio.Instance;
            audio?.Play(audio.Bank.Chime, 0.6f, 0.75f);
            PlacePhotoCamera();
        }

        void StepPhotoFinish()
        {
            if (PhotoStage == PhotoFinishStage.Playing)
            {
                photoClock = System.Math.Min(photoTo, photoClock + Time.unscaledDeltaTime * PhotoSpeed);
                if (photoClock >= photoTo) Freeze();
            }
            else if (Time.unscaledTime - photoFrozenAt > PhotoHold)
            {
                EndPhotoFinish();
                return;
            }

            foreach (var replay in replays) replay?.Show(photoClock);
            PlacePhotoCamera();
        }

        /// <summary>
        /// Low on the finish line, just inside the near post across the river from the two rafts, turning to keep the leader
        /// in the frame until it looks straight across the line (the near post stays behind it).
        /// </summary>
        void PlacePhotoCamera()
        {
            var camera = Camera.main;
            if (camera == null || replays.Count <= photoWinner || replays[photoWinner] == null) return;
            var river = course.River;
            var finish = courses[venue - 1].gates[^1];
            var line = river.At(finish);
            var leader = river.Project(replays[photoWinner].RenderPosition);
            var other = photoRunnerUp < replays.Count && replays[photoRunnerUp] != null ? river.Project(replays[photoRunnerUp].RenderPosition) : leader;
            var across = (leader.Lateral + other.Lateral) * 0.5f;
            var side = across >= 0f ? -1f : 1f;
            var focusAt = Mathf.Clamp(Mathf.Max(leader.Distance, other.Distance) + 1f, finish - 18f, finish);
            var focus = river.At(focusAt);
            var eye = line.Centre + line.Left * (side * (line.HalfWidth - 0.4f)) + line.Tangent * 0.6f;
            camera.transform.position = SimSpace.ToWorld(eye, 1.6f);
            camera.transform.LookAt(SimSpace.ToWorld(focus.Centre + focus.Left * (across * 0.6f), 0.5f));
            camera.fieldOfView = 42f;
        }

        void Freeze()
        {
            photoClock = photoTo;
            foreach (var replay in replays) replay?.Show(photoClock);
            PhotoStage = PhotoFinishStage.Frozen;
            photoFrozenAt = Time.unscaledTime;
            photoLook ??= PhotoFinishLook.Create();
            photoLook.Weight = 1f;
            var camera = Camera.main;
            if (camera != null)
            {
                savedPost = Looks.HasPostProcessing(camera);
                Looks.PostProcessing(camera, true); // the sepia even on Low, for these few seconds
            }

            var audio = GameAudio.Instance;
            audio?.Play(audio.Bank.Shutter, 1f);
            StartCoroutine(TakePhoto());
        }

        IEnumerator TakePhoto()
        {
            yield return new WaitForSecondsRealtime(0.45f); // once the flash has gone
            yield return new WaitForEndOfFrame();
            if (PhotoStage != PhotoFinishStage.Frozen) yield break;
            if (PhotoPicture != null) Destroy(PhotoPicture);
            PhotoPicture = ScreenCapture.CaptureScreenshotAsTexture();
        }

        void EndPhotoFinish()
        {
            PhotoStage = PhotoFinishStage.None;
            for (var raft = 0; raft < replays.Count; raft++)
                if (replays[raft] != null && ViewOf(raft) is { } view && RaftState(raft) is { } live)
                    view.Source = live;
            replays.Clear();
            if (photoLook != null) photoLook.Weight = 0f;
            var camera = Camera.main;
            if (camera != null)
            {
                Looks.PostProcessing(camera, savedPost);
                camera.fieldOfView = savedFieldOfView;
            }

            GameUi.CameraHeld = false;
            if (chase != null && ViewedBoat is { } viewed)
            {
                chase.enabled = Ui != null && Ui.Flow.Screen == AppScreen.Race;
                chase.SnapTo(viewed.RenderPosition, viewed.RenderAngleRad);
            }
        }
    }
}
