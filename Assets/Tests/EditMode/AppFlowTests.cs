using NUnit.Framework;
using RushAhead.Net;
using RushAhead.UI;

namespace RushAhead.Tests
{
    public class AppFlowTests
    {
        AppFlow flow;
        int ended;

        [SetUp]
        public void SetUp()
        {
            flow = new AppFlow();
            ended = 0;
            Game(SessionKind.None);
        }

        void Game(SessionKind session, RoomPhase phase = RoomPhase.Lobby, bool ready = true) => flow.Sync(session, ready, phase, ended, null);

        void End(string reason)
        {
            ended++;
            flow.Sync(SessionKind.None, true, RoomPhase.Lobby, ended, reason);
        }

        void ToMenu()
        {
            flow.TitleDone();
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.MainMenu));
        }

        [Test]
        public void Solo_GoesFromTheLobby_ThroughTheRace_ToTheResults_AndBack()
        {
            ToMenu();
            Game(SessionKind.Solo);
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Lobby));
            Game(SessionKind.Solo, RoomPhase.Racing);
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Race));
            Game(SessionKind.Solo, RoomPhase.Results);
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Results));
            Game(SessionKind.Solo);
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Lobby));
        }

        [Test]
        public void Joining_ShowsConnecting_ThenWhateverTheHostIsDoing()
        {
            ToMenu();
            flow.OpenJoin();
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Join));
            Game(SessionKind.Joined, ready: false);
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Connecting));
            Game(SessionKind.Joined, RoomPhase.Racing);
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Race), "a late joiner lands in the race");
        }

        [Test]
        public void AJoinThatFails_GoesBackToJoin_WithTheReason()
        {
            ToMenu();
            flow.OpenJoin();
            Game(SessionKind.Joined, ready: false);
            End("Couldn't reach a host at 10.0.0.9.");
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Join));
            Assert.That(flow.JoinError, Does.Contain("Couldn't reach"));
            Assert.That(flow.Banner, Is.Null);
        }

        [Test]
        public void LosingTheHostMidRace_GoesToTheMainMenu_WithABanner()
        {
            ToMenu();
            flow.OpenJoin();
            Game(SessionKind.Joined, RoomPhase.Racing);
            flow.Pause();
            End("Lost the connection to the host.");
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.MainMenu));
            Assert.That(flow.Banner, Does.Contain("Lost the connection"));
            Assert.That(flow.Paused, Is.False);
            flow.DismissBanner();
            Assert.That(flow.Banner, Is.Null);
        }

        [Test]
        public void LeavingByChoice_ShowsNoBanner()
        {
            ToMenu();
            Game(SessionKind.Hosting);
            End(null);
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.MainMenu));
            Assert.That(flow.Banner, Is.Null);
        }

        [Test]
        public void YourAnimal_StaysOpenOverTheLobby_ButTheStartTakesYouToTheRace()
        {
            ToMenu();
            Game(SessionKind.Hosting);
            flow.OpenAnimal();
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.YourAnimal));
            Game(SessionKind.Hosting, ready: true); // nothing changed
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.YourAnimal));
            Game(SessionKind.Hosting, RoomPhase.Racing);
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Race), "the start closes it");
            flow.Close();
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Race), "nothing left to close");
        }

        [Test]
        public void ClosingYourAnimal_ReturnsToWhereItOpened()
        {
            ToMenu();
            flow.OpenAnimal();
            flow.Close();
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.MainMenu));

            Game(SessionKind.Solo);
            flow.OpenAnimal();
            flow.Close();
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Lobby));
        }

        [Test]
        public void Settings_FromThePauseMenu_ReturnsToThePausedRace()
        {
            ToMenu();
            Game(SessionKind.Solo, RoomPhase.Racing);
            flow.OpenSettings();
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Race), "settings only from the pause menu in a race");
            flow.Pause();
            flow.OpenSettings();
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Settings));
            flow.Close();
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Race));
            Assert.That(flow.Paused, Is.True);
        }

        [Test]
        public void PausingOnlyHappensInARace_AndTheResultsUnpause()
        {
            ToMenu();
            flow.Pause();
            Assert.That(flow.Paused, Is.False);
            Game(SessionKind.Solo, RoomPhase.Racing);
            flow.Pause();
            Assert.That(flow.Paused, Is.True);
            Game(SessionKind.Solo, RoomPhase.Results);
            Assert.That(flow.Paused, Is.False);
        }

        [Test]
        public void TheBackButton_PausesStepsBackOrAsks()
        {
            Assert.That(flow.Back(), Is.EqualTo(BackAction.Handled), "skips the title");
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.MainMenu));
            Assert.That(flow.Back(), Is.EqualTo(BackAction.None));

            flow.OpenHost();
            Assert.That(flow.Back(), Is.EqualTo(BackAction.Handled));
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.MainMenu));

            flow.OpenJoin();
            Game(SessionKind.Joined, ready: false);
            Assert.That(flow.Back(), Is.EqualTo(BackAction.CancelConnecting));

            Game(SessionKind.Joined);
            Assert.That(flow.Back(), Is.EqualTo(BackAction.ConfirmLeave));

            Game(SessionKind.Joined, RoomPhase.Racing);
            Assert.That(flow.Back(), Is.EqualTo(BackAction.Handled));
            Assert.That(flow.Paused, Is.True);
            flow.Back();
            Assert.That(flow.Paused, Is.False);
        }

        [Test]
        public void AQuickStart_SkipsTheMenus_OnceTheTitleIsDone()
        {
            Game(SessionKind.Solo, RoomPhase.Racing);
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Title), "the title stays up its time");
            flow.TitleDone();
            Assert.That(flow.Screen, Is.EqualTo(AppScreen.Race));
        }
    }
}
