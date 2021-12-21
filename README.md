# PushIt
Rearrange furniture and other items by pushing, pulling, and rotating

For a more thorough, no-holds-barred method of object rearrangement, see the Telekinesis plugin.

# Commands

 - /push -- Allows for push, pull and rotate of the object in front of you

# Configuration
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

# Howto

 - Walk up to the object you want to move and type /push in chat
 - Use the LMB (fire) to push, RMB to pull
 - Use the R key (reload) to rotate, and shift-R to rotate the other direction
 - When done, type /push again

The plugin should detect when the object you are pushing is running into construction or other deployables, etc.  However, this only works for push and pull but not rotate (currently).

Some objects simply do not work well, such as storage boxes.  Normally, once moved, if you leave the area for some time and come back, e.g. via teleport, the box will have been moved as desired.  This is a work in progress.

Building blocks and doors are explicity blocked.

Workbenches and water containers move well.

You can even move road signs.  Why?  I don't know.

