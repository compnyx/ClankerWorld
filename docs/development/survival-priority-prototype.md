---
title: Survival priority prototype measurements
type: prototype-report
status: proposal
updated: 2026-09-30
---

# Survival priority prototype measurements

This is experimental evidence for [#378](https://github.com/compoodment/ClankerWorld/issues/378),
not approved final balance. The [agreed starting references](../game-design/world.md#starting-survival-balance-for-playtesting)
remain 40% fullness / 60% warmth, with urgent food below 20% and urgent warmth
below 35% during continued exposure. [#140](https://github.com/compoodment/ClankerWorld/issues/140)
stays open. There is no approved target action share.

## Method and limitations

`SurvivalPriorityPrototypeTests.ReportFixedSeedSurvivalPriorities` uses the normal
private-world decision and settlement paths with the real deterministic provider.
Three compatibility-map worlds use seeds `survival-priority-0`, `-1`, and `-2`.
After three idle-provider setup ticks, each starts with identical food stock (32),
45% fullness and 55% warmth. Each runs 360 ticks with fixed clear, rain or storm
weather respectively. Seasonal profiles are held constant to isolate priorities;
this does not include the separate regional-weather proposal. The baseline is
main `77f2aaf`; the prototype uses the same fixture and seeds.

These are short, small-map controlled runs, not Windows playtests, large generated
worlds, model-backed social behavior, or statistical proof of balance. Selected
**decision shares are not elapsed-time shares**: long projects and travel can
continue without a new choice. Different numbers of decisions are reported.
No model service or paid calls are used. Each final state is encoded and restored.

## Selected decisions

Shares within each run; parentheses give counts. “Other” includes hauling,
production, idle and activities outside the four measured categories.

| Weather | Version | Decisions | Survival | Social/care | Building | Exploration | Other |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Clear | Before | 115 | 13.9% (16) | 7.0% (8) | 20.0% (23) | 4.3% (5) | 54.8% (63) |
| Clear | Prototype | 101 | 7.9% (8) | 7.9% (8) | 22.8% (23) | 8.9% (9) | 52.5% (53) |
| Rain | Before | 123 | 27.6% (34) | 7.3% (9) | 19.5% (24) | 4.1% (5) | 41.5% (51) |
| Rain | Prototype | 88 | 38.6% (34) | 8.0% (7) | 23.9% (21) | 3.4% (3) | 26.1% (23) |
| Storm | Before | 114 | 48.2% (55) | 3.5% (4) | 19.3% (22) | 3.5% (4) | 25.4% (29) |
| Storm | Prototype | 154 | 47.4% (73) | 0.6% (1) | 13.6% (21) | 13.6% (21) | 24.7% (38) |

## Outcomes

Food is initial/minimum/final edible inventory. Illness is the highest individual
illness reached, not a count of sick agents. No deaths occurred, so these short
runs cannot establish preventable-death risk. Project completions include
construction and production, not only Houses.

| Weather | Version | Food | Peak illness | Deaths | Meals | Exploration moves | Project completions |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| Clear | Before | 32/24/24 | 0.00% | 0 | 8 | 29 | 20 |
| Clear | Prototype | 32/28/28 | 0.00% | 0 | 4 | 83 | 21 |
| Rain | Before | 32/24/24 | 0.00% | 0 | 8 | 34 | 23 |
| Rain | Prototype | 32/28/28 | 2.64% | 0 | 4 | 21 | 17 |
| Storm | Before | 32/24/24 | 2.64% | 0 | 8 | 28 | 19 |
| Storm | Prototype | 32/28/28 | 16.84% | 0 | 4 | 49 | 19 |

## Available but unchosen opportunities

Counts of decisions where a category was offered but a different category won.
A decision can count in several columns; these are opportunities, not blocked
agent time or proof that every offered action would complete.

| Weather | Version | Social/care | Building | Exploration | Other |
| --- | --- | ---: | ---: | ---: | ---: |
| Clear | Before | 49 | 28 | 72 | 52 |
| Clear | Prototype | 53 | 30 | 66 | 48 |
| Rain | Before | 73 | 41 | 76 | 72 |
| Rain | Prototype | 41 | 28 | 47 | 65 |
| Storm | Before | 40 | 46 | 71 | 85 |
| Storm | Prototype | 33 | 45 | 72 | 116 |

## Interpretation and next playtest

The comfortable references no longer veto safe construction, curiosity or nearby
care. Food remains available, and fewer meals are taken. Clear-weather survival
choice share falls and exploration grows. **This does not solve weather pressure:**
rain survival share rises, rain exploration falls, and storm social/care choices
fall. Peak illness increases in both wet-weather runs; the storm increase is
material. No-death results over 360 ticks are not reassurance about longer runs.

The prototype uses provisional routine food seeking below 45%, carried eating
below 40%, and a 30% reserve for starting exploration. These are not additional
owner decisions. Food production, illness rates, exposure physics, no-energy /
no-sleep behavior and save schema are unchanged. Safe nearby responses remain
possible under urgent food; long food-gathering trips for child care do not.

Do not call this settled balance or close #140. Before adopting the tuning,
playtest longer wet-weather runs and inspect whether warmth choices make useful
progress, whether outings leave adequate protection, and whether nearby social
opportunities actually get selected. Compare generated worlds and household
food production as well as these small controlled fixtures. The prototype is
reviewable code and measurements, not deployment or native game verification.
