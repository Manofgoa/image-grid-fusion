# Soundtrack

> Working document — force a soundtrack over the whole grid, mixed over the videos' own sound.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Let the user force a **soundtrack** on the grid: a **global effect** (not tied to a cell + image
pair), whose source can be a **video or an audio file**. It is **mixed over** the sound of the
grid's videos rather than replacing it. Each video's own sound can still be turned off
individually if the user wants.

Initial request (user, in French):

> Pouvoir forcer une bande son. Effet global. Ça peut être une vidéo ou un audio. Ça se met par
> dessus les sons des autres vidéos (mixage). On peut si on veut aller désactiver le son de
> chaque vidéo.

---

## Scoping

Questions the design depends on, split by who answers them cheapest.

| Question | Answered by |
|---|---|
| Where the soundtrack is heard: preview, exports, or both | the user |
| What happens when the soundtrack and the grid's duration differ | the user |
| Which volume controls the mix offers | the user |
| Expected depth of the exploration | the user |
| How the existing global effects (global fade, background) are exposed in the UI | the codebase |
| Whether per-video mute already exists, and how | the codebase |
| How audio is played in the preview and whether exports carry audio today | the codebase |

---

## Test Impact

To be filled during design.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|

---

## Open Questions

To be listed once scoping and exploration are done.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where is the soundtrack heard: preview, exports, or both? | Preview and video exports; GIF and still exports stay silent | 2026-09-25 |
| 2 | When the soundtrack and the grid's duration differ, what happens? | The grid's duration rules: a shorter soundtrack loops, a longer one is cut | 2026-09-25 |
| 3 | Which volume controls does the mix offer? | One soundtrack volume slider in the effect's options; videos keep their level, each one can be muted | 2026-09-25 |
| 4 | Is the subject expected to be straightforward, or tricky / long? | Straightforward — a single scout pass | 2026-09-25 |

---

*Last updated: 2026-09-25*
