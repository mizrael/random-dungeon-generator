# Random Dungeon Generator

## DISCLAIMER
I haven't written this code, I found it online almost 10 years ago and integrated in a small XNA game I was working on at the time. The game was never completed and the source were buried in an old hard-drive.

**I don't own these sources so if you're the author and you want them removed just let me know.**

## Sample usage

```
var generator = new DungeonGenerator.DungeonGenerator(9, 7, 40, 40, 60, new DungeonGenerator.RoomGenerator(4, 2, 3, 2, 3)); 

var dungeon = generator.Generate();
```
