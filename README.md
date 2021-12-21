# PushIt
Rearrange furniture and other items by pushing, pulling, and rotating

For a more thorough, no-holds-barred method of object rearrangement, see the Telekinesis plugin.

Permission is required to use, but it is recommended that you either have the RemoverTool plugin handy or only give rights to admins in case you need to repair a problem.

For items that can be picked up anyway, if they get stuck inside a wall for some reason they can be removed easily without another plugin.  But, keep this potential problem in mind for items that cannot be picked up.

<p align="center">
<iframe width="1280" height="720" src="https://www.youtube.com/embed/w-wzVZowWdE" title="YouTube video player" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowfullscreen></iframe>
</p>

## Commands

 - /push -- Allows for push, pull and rotate of the object in front of you

## Permission

 - pushit.use -- Allows use of the /push command

## Configuration
```json
{
  "Disallow building blocks, doors, and windows": true,
  "Disallow other things that can cause trouble": true,
  "Minimum distance to maintain to the target": 5.0,
  "debug": true,
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 1
  }
}
```
Blocks includes any building block as well as any entity matching door, fence, hatch, bars, cell, shopfront, or reinforced.

The list of other things currently only includes anything matching "vehicle".

## HowTo

 - Put away your weapons and hammer, etc.
 - Walk up to the object you want to move and type /push in chat
 - Use the LMB (fire) to push, RMB to pull
 - Use the R key (reload) to rotate clockwise, and shift-R to rotate counter-clockwise (looking down from above)
 - When done, type /push again

Note that the push and pull action moves directly in line from the player position through the object center position and without any regard for how your building is laid out.  In other words, it does not move in parallel to any structures unless you happen to be perfectly aligned to them as well.

The plugin should detect when the object you are pushing is running into construction or other deployables, etc.  However, this only works for push and pull but not rotate (currently).

The edge detection checks along the side opposite from where you are standing.  However, the vector only extends from the middle of each edge and not along the full width, so this can occasionally fail to work as desired.

Some objects simply do not work well, such as storage boxes.  Normally, once moved, if you leave the area for some time and come back, e.g. via teleport, the box will have been moved as desired.  This is a work in progress.

Building blocks and doors are explicity blocked.

Workbenches and water containers move well.

I had seen with Telekinesis that vehicle modules could be moved relative to the rest of the car, which made things... interesting.  So, they should be blocked by default here.

You can even move road signs.  Why?  I don't know.

