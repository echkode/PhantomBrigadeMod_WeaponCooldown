# WeaponCooldown

A library mod for [Phantom Brigade](https://braceyourselfgames.com/phantom-brigade/) that adds a new per-weapon cooldown mechanic.

It is compatible with game release version **1.1.3**. It works with both the Steam and Epic installs of the game. All library mods are fragile and susceptible to breakage whenever a new version is released.

If you have installed and enabled any of my previous mods for the combat timeline from my [Fixes](https://github.com/echkode/PhantomBrigadeMod_Fixes) repo, **you must disable those mods before enabling this one.** Those mods and this one modify some of the same areas of code and therefore cannot coexist.

This mod also includes a number of behind-the-scenes fixes for the combat timeline that touch up some of the UX and reduce the chance of the game unexpectedly exiting when dragging actions.

Credit for the idea goes to Ragvard from the BYG Discord server who [posted the idea](https://discord.com/channels/380929397445754890/1136131772082556968/1136131772082556968) in the Phantom Brigade Feedback forum. This mod is my interpretation of that idea.

This readme is divided into sections so you can quickly jump to what you're looking for. Keep in mind when you read this document that I refer to the per-weapon cooldown mechanic by its technical term, activation lockout or lockout. Conceptually it can be used for more than literal cooling down. For example, it may represent a long reloading time for something like an old-fashioned tank cannon or the recharging time for the flux capacitors of a railgun.

Sections
- [Screenshots](#screenshots)
- [How to use with your mod](#how-to-use)
- [Fixes](#fixes)
- [Technical Notes](#technical-notes)

## Screenshots
Here are some screenshots and video captures of the mod in action.

A unit can still run and use the alternate attack action in the lockout duration.
![Different attack actions, each with lockout](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/dacbc2c6-0820-4683-bc7a-58eb7e08a321)

A unit can dash in the lockout duration.
![Unit dashing in the lockout duration](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/d1e9e9f4-a83d-4b5f-b351-15e220058580)

The combat AI is aware of lockout. Some weapons like the marksman rifle are not meant to be fired while moving. The enemy unit will wait for the duration of the attack and then begin moving in the lockout duration. Lockout won't freeze the enemy. The next attack with that weapon will happen after the lockout duration.

![An enemy unit waits while firing a marksman rifle and then moves in the subsequent lockout duration](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/ac597327-4663-44bc-a7dc-091f96201942)

Enemy units can also dash in the lockout duration.

![Enemy unit moving and dashing in the lockout duration](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/e5d5fb99-2714-4bfd-bbe9-0e9d264723d9)

Dragging actions with lockout will respect the lockout. Actions that are dragged can push neighboring actions. Similar actions will begin to move when either their main action durations (the solid part of the action) or their lockout durations (the diagonal lines) touch. Different actions will only move when the main durations touch.

<video controls src="https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/9378c377-ea7e-4958-838c-866a0a658baa">
  <p>Dragging different kinds, some with lockout and some without.</p>
</video>

## How to Use

This mod doesn't do much by itself. It provides a new mechanic that other modders can use in their mods. If you are a modder and want to add cooldowns to your weapon, here's how to do it.

To add activation lockout to an existing weapon, you will need to create a ConfigEdit for the weapon's primary activation subsystem. You need to make sure the weapon is properly configured with a primary activation subsystem. Here's how to find that subsystem.

A weapon is built up from several different configs. At the top level is the part preset config which contains a list of steps to generate the part (`genSteps`). A weapon should have a `AddHardpoints` genSteps entry that targets the `internal_main_equipment` hardpoint as shown in this screenshot.

![A part preset config with a AddHardpoints genStep targeting the internal_main_equipment hardpoint](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/ba7f937e-1eb0-451d-b24e-dc79365e0387)

One of the subsystems listed in the genStep under the `subsystemsInitial` field should be a weapon subsystem key. These keys usually start with the prefix `wpn_main_`. In this example, the weapon subsystem key is `wpn_main_mg_03`.

All properly configured part presets for weapons will have a single subsystem targeting the `internal_main_equipment` hardpoint and the config for the subsystem will have an activation section. This is an example of a subsystem config with an activation section.

![A subsystem config with an activation section](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/1695c5a1-9e4a-4e7a-8121-8232e67efe92)

Here's what it looks like when the subsystem config does not have an activation section.

![A subsystem config without an activation section](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/f514f8c5-8ef8-47e6-8e0d-60633b3ac5e4)

This mod only works with subsystems that have activation sections. This means it does not work with thrusters. Don't try to hack in an activation for them. That will break things badly because neither the game code nor my mod is expecting `internal_aux_*` subsystems to have activation.

The next thing you need to look for in the subsystem config is a custom section. Most weapons do not have a custom section and that will look like the following screenshot.

![A subsystem config without a custom section](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/65eff4a6-e216-4dac-97fd-04b024a77071)

Melee weapons have a custom section.

![A melee subsystem config with a custom section](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/7ae2f323-4cb0-4228-a3e6-3c5e9e69463e)

This is important because it will change how you write your ConfigEdit to patch in activation lockout for your weapon.

Now that you know what the primary activation subsystem is, you'll need to create the ConfigEdit file. It will look different depending on the custom section of the subsystem config.

For subsystems without a custom section, your ConfigEdit will need to first create the custom objects. Here's how it should look.

![A ConfigEdit for a subsystem without a custom section](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/217f7718-7117-479c-81cf-3a1d3ac4a9d6)

For subsystems with custom sections, you simply need to add a new custom float entry.

![A ConfigEdit for a subsystem with a custom section](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/eee2b374-9fdd-40b4-bd4a-1bdb35782e7d)

Bundle your ConfigEdits in your own mod. Do not include them with this mod. Your mod will depend on this one, so you will need to make sure this mod is installed and both are enabled in the game.

## Fixes

Some of the fixes in this mod altered the UX. Here are a few examples.

There was no indication that a run, wait, dash or melee action is placed near the end of the turn might be too late. You could draw out the destination for a run, wait or dash but the action wouldn't be placed and there was no feedback. I've captured a few videos of this in action (or, rather, inaction).

<video controls src="https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/d4b0b414-756c-4bef-a2da-9ae9f929a763">
  <p>Attempt to place a run action too late in the turn doesn't work and doesn't show any warning.</p>
</video>

<video controls src="https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/0d265c1c-5de8-4320-b11e-7b3ea18497d6">
  <p>Attempt to place a dash action too late in the turn doesn't work and doesn't show any warning.</p>
</video>

Melee actions will get placed. In the following capture you can just see a sliver of the melee action at the end of the combat timeline. It actually starts in the next turn.

<video controls src="https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/7aa40737-0476-498a-bd49-a535812eddf7">
  <p>Attempt to place a melee action too late in the turn results in the action being placed in the next turn and only a sliver shown at the end of the combat timeline.</p>
</video>

Here's a more egregious example, where a melee action gets placed at the end of a 6.3s long run. That means it's placed at 1.3s into the next turn.

<video controls src="https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/1d69cd51-6f27-4066-be65-59e2dc7c8990
">
  <p>Placing a melee action at the end of a long run puts it well into the next turn.</p>
</video>

This mod shows a late warning toast above the timeline. Here's an example of what it looks like for a late dash action.

<video controls src="https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/37c06aec-d6ae-4413-a6f3-16b2b5be2b65">
  <p>The late warning toast pops up when a dash action is placed too late.</p>
</video>

With this mod, melee actions are restricted to being placed in the current turn and the late warning toast will appear when appropriate. No more surprise actions when the next turn rolls around.

<video controls src="https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/d4fe6b41-abc6-4613-a312-06f53ba8424d">
  <p>The late warning toast pops up when a melee action is placed too late.</p>
</video>

Here are screenshots of all the actions that trigger the late warning toasts.

![late warning toast for wait action](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/465d4f80-1bf0-4cf9-8d85-6799334d835e)
![late warning toast for run action](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/6feb9382-6250-4352-befb-4b037e0a5465)
![late warning toast for dash action](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/ec96477f-61e9-45bb-b369-daa5adcf2ecd))
![late warning toast for melee action](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/c5d88b07-e5e1-4119-a5e2-1cb2aec2813f)

Attack actions other than melee don't show the late warning toast. A different approach is used with non-melee attack actions called time placement mode. I haven't been able to find a way to get the time placement mode to allow an action to be placed too late. If you try to place an attack action and there's an existing action on the secondary track that extends past the last placement cutoff time, the overlap warning toast will pop up. This mod doesn't change that UX.

![overlap warning toast for a late non-melee attack action](https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/ee173296-1a3a-4e36-8c6e-5d1ab908aa1b)

There are a few display problems when placing an attack action in the existing code base, mostly relating to overlap. Here's a video capture that goes through some of them.

<video controls src="https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/1156d8ec-8dc2-42c3-b702-566393065bd4">
  <p>Overlap display issues in the existing code.</p>
</video>

Here's a list of the problems in the order they happen in the video.
1. Move to start of timeline, the overlap sprite grows to full size of existing action, not the one being placed.
2. The overlap sprite extends past the beginning of the existing action towards the start of the timeline.
3. Move to the end of the timeline, again size of the overlap sprite grows to that of the existing action.
4. Trying to place the new action while the timeline scrubber I-beam is over an existing action places the new action after the existing one but does not remove the overlap sprite and the overlap warning toast. The scrubber I-beam even gets moved to the end of the existing action so there is no overlap. The display is misleading.
5. Another demonstration of the problem with the second existing action.
6. This time the I-beam scrubber is over the new action when the left button is click to try to place it. The new action gets placed to before the existing action it overlaps with and the scrubber I-beam is moved to the front of the new action. Again, there is no overlap condition but the display hasn't been updated.

I captured a similar sequence of trying to place an action with the mod enabled.

<video controls src="https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/f5d18f72-ad73-4734-8061-973527feb07e">
  <p>What overlap now looks like with the mod.</p>
</video>

Below is a list of the changes this mod makes. I also point out how the new lockout feature works when placing actions. Note that I've dialed down the heat on the weapons to hide the heat UI elements and focus on the overlap UI and UX.
1. The second melee action is automatically placed at the end of the first.
2. Dragging the melee actions respects the lockout regions.
3. New actions are placed on top of existing lockouts.
4. The lockout region of new action slides under later existing actions and fades when it is under an existing action.
5. The lockout region of the new action does not cause the overlap sprite or the overlap warning toast to be displayed when it slides under an existing action.
6. The new action does not trigger any overlap display when it is over a lockout portion of an existing action.
7. The overlap sprite is displayed only when the act_duration portion of the new action begins to overlap an existing action in either direction.
8. The size of the overlap sprite is constrained to be at most the size of the new action act_duration region.
9. When dragging the new action to the start of the timeline, the overlap sprite stops at the beginning of the existing action.
10. Placing the new action while it overlaps an existing action and the scrubber I-beam is over the new action will put the new action before the existing one and hide the overlap sprite and warning toast.
11. When the placement is canceled, the overlap sprite and warning toast are re-displayed.
12. When the scrubber I-beam is over an existing action, the new action is placed after the existing one and the overlap UI elements are hidden.

Once an action is placed, you can drag the actions with lockout around and the lockout will interact (or not) with the neighboring actions. I added one change not related to lockout: you can undo a cancel by left clicking on a blank area of the timeline or another action that isn't selected for cancel.

<video controls src="https://github.com/echkode/PhantomBrigadeMod_WeaponCooldown/assets/48565771/4b13aecd-ec24-4d8b-87ca-5b7655ae9708">
  <p>Dragging actions with lockout.</p>
</video>

Some points of interest in this video capture.
1. An action can be dragged while it is over a lockout region.
2. Moving a different kind of action will not start to push an action with a lockout region until the act_duration regions touch.
3. Cancelling an action in the secondary track while it is over a lockout region will only cancel that action.
4. Cancelling double track actions will not cancel actions that are only on the secondary track even if these secondary track actions are on top of the lockout region of the double track action being cancelled.
5. Cancel can be undone by left clicking on a blank region of the timeline or another action that isn't selected for cancelling.

## Technical Notes

The combat timeline is the prime UI element in the planning phase. Each action scheduled by a unit in the turn is shown on the timeline as a separate UI element that I call a helper. Helpers are ordered by the start time of the action and are sized by the duration of the action. For attack actions, this duration can be determined by the primary activation subsystem of the weapon that's used in the attack.

Helpers are arranged in two rows on the timeline. The lower row is called the primary track and will have actions like run and wait. The upper row is called the secondary track and this is where attack, shield, retreat and eject actions go. Some actions like dash and melee are double track actions which occupy both tracks.

Actions can be placed on the timeline in one of two ways. Run and wait actions are placed automatically at the end of the last action on the primary track or at the start of the turn if there are no actions on the primary track. Secondary actions use a mode call time placement where the player can either scrub in the timeline or in the combat scene to set the start time of the action. Dash and melee actions work sort of like run and wait actions in that they are automatically placed at the end of the last action on the primary track but with a wrinkle: if they will also consider the secondary track and will try to move later in the turn to avoid overlapping actions on the secondary track.

A temporary (and reused) helper is used when a secondary action is being placed in time placement mode. This is the painted helper. Once the action is placed, the temporary helper is replaced with a more permanent helper that is unique to that action.

Actions can be dragged. A dragged action can push its neighbors back and forth on the their tracks but it cannot leapfrog any neighbor to an open space on the other side. Double track actions can push neighboring actions on either or both tracks. Actions cannot be pushed into the previous turn nor can they be pushed completely into the next turn. Secondary track and double track actions that started in the previous turn and continue into the current one cannot be dragged or pushed. Move and wait actions from the previous turn will become new actions in the current turn if they extend long enough into the turn.

Crashs are shown as double track actions that can't be cancelled, dragged or pushed.

The prime directive of the combat timeline is that actions on the same track cannot overlap. There are lots of good reasons for this rule but the point of this mod is to break that rule. There is a lot of code that deals with widget depths and helper ordering and which parts of helpers are overlapping with which parts of other helpers.

There's a second rule: all actions created in the turn must start between the turn start time and the turn end time minus a small adjustment factor. This rule is not always observed and when it isn't, the result can be an unexpected exit of the game.

Dragging actions on the combat timeline is a feature that was introduced well after much of the code for the combat timeline had been written. It implicitly relies on the above two rules to work properly. Unfortunately, some places that needed to be updated to enforce the second rule were not updated. In addition to all the code for the lockout feature proper, there are a number of spot fixes to better respect that rule.

These spot fixes are mostly done with Harmony IL transpiler patches. Working with IL is fiddly and it's nearly impossible to look at the patch code and understand what it does. I rarely comment my code but I made sure to put comments in every IL transpiler patch. IL is an assembly language and there's a reason the old assembly programmers commented every line of code.

Always, always set `Harmony.DEBUG` when tinkering in IL. I almost never get it right the first time and the generated log is absolutely necessary to figure out where the patch went bad. Once you have it dialed in, you can turn off `Harmony.DEBUG` so it doesn't generate the dump file on the user's desktop.

One of the techniques I use a lot when working with timeline objects, either actions or helpers, is to order the objects. For actions, order by start time. For helpers, order by the X value of their local position which nicely happens to be in UI space. This will ensure you work with bounded loops that have low iteration counts (the player can put only so many actions on the timeline).

One unexpected area that got touched was the combat AI. The AI generates actions without using the timeline so it isn't constrained by the lockout UI in the same way that the player is. I had to teach the AI to respect lockout periods when scheduling actions.

The combat AI is built on behavior trees. Surprisingly, enemy units don't use the AI in the execution phase of a turn. Rather, the behavior trees are used in the planning phase to generate a schedule of suggested actions. These suggestions are evaluated and actions are generated for ones that pass the tests. In the behavior trees are nodes that evaluate conditions and nodes that generate actions. One of the action generation nodes is named `BTAction_UseEquipment` and this is the node that generates attack actions. I modified this node to look at the lockout duration and not generate an attack action if the current planning time is still in that duration. I also created a new behavior node (`BTCondition_CheckWeaponReady`) to check the lockout condition explicitly. This node is injected into the behavior trees at the appropriate places when they're loaded.
