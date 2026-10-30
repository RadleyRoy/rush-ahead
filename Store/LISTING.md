# Google Play listing for Rush Ahead

Text to paste into the Play Console, and draft answers for its forms. **The form answers are a starting point
from how the game is built: read each one and check it against the Play Console's own wording before you
submit.** Graphics are in `Store/Graphics`.

## Main store listing

**App name** (30 characters max):

> Rush Ahead

**Short description** (80 characters max, 75 used):

> Steer one raft with friends and leaf blowers. Race rivers solo or together!

**Full description** (4,000 characters max):

> Four animals. One raft. Four leaf blowers. No steering wheel.
>
> Rush Ahead is a party game about steering a round inflatable raft down a river by blowing air. Every animal
> on board holds a blower and owns a slice of directions around the raft: air goes one way, the raft goes the
> other. Push together and you fly. Push against each other and you spin, wobble and capsize.
>
> PLAY TOGETHER
> - Up to 4 players on one raft in Co-op, or up to 8 in Team Race: Red raft against Blue
> - Play on the same Wi-Fi or a phone's hotspot, no internet needed
> - Or play online: host a game and share the six-letter code
> - AI crewmates fill any empty seat, from Clumsy to Ace, and take over if someone drops out
>
> SIX COURSES, THREE PLACES
> - Wind down creeks, cross calm lakes and fight through white-water rapids
> - Dodge rocks, shove drifting logs and barrels aside, and keep clear of whirlpools
> - Earn stars to open new courses, and chase your best time with live checkpoint splits
>
> YOUR ANIMAL, YOUR BLOWER
> - Pick a bear, cat, bunny, pig, otter or raccoon in twelve fur colours
> - Choose your blower: the all-round leaf blower, the quick hair dryer, the burst-only bellows, the instant hand
>   fan, or the shop vac that sucks instead of blows
>
> Give your crew orders (Go, Hold, Brace, Push here), burst out of trouble, and brace when the raft starts to tip.
>
> No ads. No purchases. No accounts.

**App category:** Game > Racing (or Casual)

**Tags** (pick up to 5 in the console): Racing, Party, Multiplayer, Co-op, Casual

**Contact details:** your email (required); website optional.

**Privacy policy URL:** the address where `Store/PRIVACY.md` is published (see `RELEASE.md`).

## Graphics (in `Store/Graphics`)

| Asset | File | Play's requirement |
|---|---|---|
| App icon | `icon-512.png` | 512 x 512 PNG |
| Feature graphic | `feature-graphic.png` | 1024 x 500 PNG or JPEG |
| Phone screenshots | `screenshot-*.jpg` | 2 to 8, 16:9 landscape at 1920 x 1080 |

## App content (Policy > App content)

**Ads:** No, the app doesn't contain ads.

**App access:** All functionality is available without special access (no login).

**Target audience and content:** ages 13 to 15, 16 to 17, and 18 and over. Rush Ahead has cartoon animals, so
Play may ask whether it unintentionally appeals to children; answer that it isn't designed for them (no
child-directed marketing, the competition and online play are pitched at teens and adults). If you'd rather
include under-13s, the game would need to join the Families programme, which affects online play: talk it
through before choosing.

**Content rating (IARC questionnaire)**, likely answers:

- Category: Game
- Violence: none (rafts bump into rocks and each other; nobody is hurt)
- Fear, sexuality, language, drugs, gambling: none
- Users can interact or exchange information: **Yes**. Players see each other's chosen animal names in online
  and Wi-Fi games. There's no chat.
- Shares the user's location: No
- Digital purchases: No

Expect a rating around PEGI 3 / ESRB Everyone, with an "Users Interact" note.

**Data safety**, draft answers:

- Does the app collect or share any of the required user data types? **Yes**, only for online play, through
  Unity Gaming Services (Unity processes it for the game as a service provider, which counts as collection, not
  sharing).
- Is all user data encrypted in transit? **Yes**. Online games go through Unity Relay over DTLS (Unity's default
  on Android). Wi-Fi games stay on the local network.
- Can users ask for their data to be deleted? There's no account: the sign-in is anonymous and kept only on the
  phone, so uninstalling removes it. Say so in the deletion question if it asks.

| Data type | Collected | Shared | Ephemeral | Required? | Why |
|---|---|---|---|---|---|
| Personal info > Name (the animal's name, a nickname) | Yes | No | Yes | Optional (online play only) | App functionality |
| Device or other IDs (anonymous Unity player ID) | Yes | No | No | Optional (online play only) | App functionality |
| App activity > Other actions (controls and raft positions relayed during a game) | Yes | No | Yes | Optional (online play only) | App functionality |

Everything else (location, contacts, photos, audio, financial, health, messages, web browsing, app info and
performance, crash logs) is **not collected**: Unity's analytics, crash and performance reporting are all
switched off in this project.

**Government apps, financial features, health, news, COVID-19:** not applicable.
