using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DungeonGenerator
{
    public class DungeonGenerator
    {
        #region Fields

        private int width = 25;
        private int height = 25;
        private int changeDirectionModifier = 30;
        private int sparsenessModifier = 70;
        private int deadEndRemovalModifier = 50;
        private readonly RoomGenerator roomGenerator = new RoomGenerator(10, 1, 5, 1, 5);

        #endregion

        #region Constructors

        public DungeonGenerator()
        {

        }

        public DungeonGenerator(int width, int height, int changeDirectionModifier, int sparsenessModifier, int deadEndRemovalModifier, RoomGenerator roomGenerator)
        {
            this.width = width;
            this.height = height;
            this.changeDirectionModifier = changeDirectionModifier;
            this.sparsenessModifier = sparsenessModifier;
            this.deadEndRemovalModifier = deadEndRemovalModifier;
            this.roomGenerator = roomGenerator;
        }

        #endregion

        #region Methods

        public Dungeon Generate()
        {
            Dungeon dungeon = new Dungeon(width, height);
            dungeon.FlagAllCellsAsUnvisited();

            CreateDenseMaze(dungeon);
            SparsifyMaze(dungeon);
            RemoveDeadEnds(dungeon);
            roomGenerator.PlaceRooms(dungeon);
            roomGenerator.PlaceDoors(dungeon);

            return dungeon;
        }

        public void CreateDenseMaze(Dungeon dungeon)
        {
            Point currentLocation = dungeon.PickRandomCellAndFlagItAsVisited();
            DirectionType previousDirection = DirectionType.North;

            while (!dungeon.AllCellsAreVisited)
            {
                DirectionPicker directionPicker = new DirectionPicker(previousDirection, changeDirectionModifier);
                DirectionType direction = directionPicker.GetNextDirection();

                while (!dungeon.HasAdjacentCellInDirection(currentLocation, direction) || dungeon.AdjacentCellInDirectionIsVisited(currentLocation, direction))
                {
                    if (directionPicker.HasNextDirection)
                        direction = directionPicker.GetNextDirection();
                    else
                    {
                        currentLocation = dungeon.GetRandomVisitedCell(currentLocation); // Get a new previously visited location
                        directionPicker = new DirectionPicker(previousDirection, changeDirectionModifier); // Reset the direction picker
                        direction = directionPicker.GetNextDirection(); // Get a new direction
                    }
                }

                currentLocation = dungeon.CreateCorridor(currentLocation, direction);
                dungeon.FlagCellAsVisited(currentLocation);
                previousDirection = direction;
            }
        }

        public void SparsifyMaze(Dungeon dungeon)
        {
            // Calculate the number of cells to remove as a percentage of the total number of cells in the dungeon
            int noOfDeadEndCellsToRemove = (int)Math.Ceiling(((decimal)sparsenessModifier / 100) * (dungeon.Width * dungeon.Height));

            IEnumerator<Point> enumerator = dungeon.DeadEndCellLocations.GetEnumerator();

            for (int i = 0; i < noOfDeadEndCellsToRemove; i++)
            {
                if (!enumerator.MoveNext()) // Check if there is another item in our enumerator
                {
                    enumerator = dungeon.DeadEndCellLocations.GetEnumerator(); // Get a new enumerator
                    if (!enumerator.MoveNext()) break; // No new items exist so break out of loop
                }

                Point point = enumerator.Current;
                dungeon.CreateWall(point, dungeon[point].CalculateDeadEndCorridorDirection());
                dungeon[point].IsCorridor = false;
            }
        }

        public void RemoveDeadEnds(Dungeon dungeon)
        {
            foreach (Point deadEndLocation in dungeon.DeadEndCellLocations)
            {
                if (ShouldRemoveDeadend())
                {
                    Point currentLocation = deadEndLocation;

                    do
                    {
                        // Initialize the direction picker not to select the dead-end corridor direction
                        DirectionPicker directionPicker = new DirectionPicker(dungeon[currentLocation].CalculateDeadEndCorridorDirection(), 100);
                        DirectionType direction = directionPicker.GetNextDirection();

                        while (!dungeon.HasAdjacentCellInDirection(currentLocation, direction))
                        {
                            if (directionPicker.HasNextDirection)
                                direction = directionPicker.GetNextDirection();
                            else
                                throw new InvalidOperationException("This should not happen");
                        }
                        // Create a corridor in the selected direction
                        currentLocation = dungeon.CreateCorridor(currentLocation, direction);

                    } while (dungeon[currentLocation].IsDeadEnd); // Stop when you intersect an existing corridor.
                }
            }
        }

        public bool ShouldRemoveDeadend()
        {
            return Random.Instance.Next(1, 99) < deadEndRemovalModifier;
        }

        public static int[,] ExpandToTiles(Dungeon dungeon, int tileStep)
        {
            int w = tileStep * (dungeon.Width * 2 + 1);
            int h = tileStep * (dungeon.Height * 2 + 1);

            // Instantiate our tile array
            int[,] tiles = new int[w, h];

            // Initialize the tile array to rock
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tiles[x, y] = (int)TileType.Void;

            // Fill tiles with corridor values for each room in dungeon
            foreach (Room room in dungeon.Rooms)
            {
                // Get the room min and max location in tile coordinates
                Point minPoint = new Point(tileStep * (room.Bounds.Location.X * 2 + 1), tileStep * (room.Bounds.Location.Y * 2 + 1));
                Point maxPoint = new Point(tileStep * room.Bounds.Right * 2, tileStep * room.Bounds.Bottom * 2);

                // Fill the room in tile space with an empty value
                for (int i = minPoint.X; i < maxPoint.X; i++)
                    for (int j = minPoint.Y; j < maxPoint.Y; j++)
                        tiles[i, j] = (int)TileType.Empty;

                //tiles[minPoint.X - 1, minPoint.Y - 1] = (int)TileType.WallAngleSE;
                //tiles[minPoint.X - 1, maxPoint.Y] = (int)TileType.WallAngleNE;
                //tiles[maxPoint.X, minPoint.Y - 1] = (int)TileType.WallAngleSO;
                //tiles[maxPoint.X, maxPoint.Y] = (int)TileType.WallAngleNO;
            }

            int doorSize = (int)Math.Ceiling((decimal)tileStep / 2);
            int startStep = (int)Math.Ceiling((decimal)doorSize / 2);

            // Loop for each corridor cell and expand it
            foreach (Point cellLocation in dungeon.CorridorCellLocations)
            {
                Point tileLocation = new Point(tileStep * (cellLocation.X * 2 + 1), tileStep * (cellLocation.Y * 2 + 1));

                for (int x = tileLocation.X; x != tileLocation.X + tileStep; ++x)
                    for (int y = tileLocation.Y; y != tileLocation.Y + tileStep; ++y)
                        tiles[x, y] = (int)TileType.Empty;

                if (dungeon[cellLocation].SouthSide == SideType.Empty)
                {
                    for (int x = tileLocation.X; x != tileLocation.X + tileStep; ++x)
                        for (int y = tileLocation.Y + tileStep; y != tileLocation.Y + tileStep * 2 + 1; ++y)
                            tiles[x, y] = (int)TileType.Empty;
                }
                if (dungeon[cellLocation].EastSide == SideType.Empty)
                {
                    for (int x = tileLocation.X + tileStep; x != tileLocation.X + 1 + 2 * tileStep; ++x)
                        for (int y = tileLocation.Y; y != tileLocation.Y + tileStep; ++y)
                            tiles[x, y] = (int)TileType.Empty;
                }

                if (dungeon[cellLocation].NorthSide == SideType.Door)
                {
                    for (int x = tileLocation.X; x != tileLocation.X + tileStep; ++x)
                        for (int y = tileLocation.Y - tileStep - 1; y != tileLocation.Y; ++y)
                            tiles[x, y] = (int)TileType.Empty;

                    for (int x = tileLocation.X; x != tileLocation.X + tileStep; ++x)
                        tiles[x, tileLocation.Y - 1] = (int)TileType.Wall;

                    for (int x = tileLocation.X + startStep; x != tileLocation.X + startStep + doorSize; ++x)
                        tiles[x, tileLocation.Y - 1] = (int)TileType.Door;
                }
                if (dungeon[cellLocation].WestSide == SideType.Door)
                {
                    for (int x = tileLocation.X - tileStep - 1; x != tileLocation.X; ++x)
                        for (int y = tileLocation.Y; y != tileLocation.Y + tileStep; ++y)
                            tiles[x - 1, y] = (int)TileType.Empty;

                    for (int y = tileLocation.Y; y != tileLocation.Y + tileStep; ++y)
                        tiles[tileLocation.X - 1, y] = (int)TileType.Wall;

                    for (int y = tileLocation.Y + startStep; y != tileLocation.Y + startStep + doorSize; ++y)
                        tiles[tileLocation.X - 1, y] = (int)TileType.Door;
                }
                // ******************************* //
                // original code 
                //if (dungeon[cellLocation].NorthSide == SideType.Empty) tiles[tileLocation.X, tileLocation.Y - 1] = (int)TileType.Empty;
                //if (dungeon[cellLocation].NorthSide == SideType.Door) tiles[tileLocation.X, tileLocation.Y - 1] = (int)TileType.Door;

                //if (dungeon[cellLocation].SouthSide == SideType.Empty) tiles[tileLocation.X, tileLocation.Y + 1] = (int)TileType.Empty;
                //if (dungeon[cellLocation].SouthSide == SideType.Door) tiles[tileLocation.X, tileLocation.Y + 1] = (int)TileType.Door;

                //if (dungeon[cellLocation].WestSide == SideType.Empty) tiles[tileLocation.X - 1, tileLocation.Y] = (int)TileType.Empty;
                //if (dungeon[cellLocation].WestSide == SideType.Door) tiles[tileLocation.X - 1, tileLocation.Y] = (int)TileType.Door;

                //if (dungeon[cellLocation].EastSide == SideType.Empty) tiles[tileLocation.X + 1, tileLocation.Y] = (int)TileType.Empty;
                //if (dungeon[cellLocation].EastSide == SideType.Door) tiles[tileLocation.X + 1, tileLocation.Y] = (int)TileType.Door; 
                // ******************************* //
            }

            // mark corners
            for (int x = 1; x < w-1; x++)
            {
                for (int y = 1; y < h-1; y++)
                {                    
                    if ( TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x , y - 1]) &&
                         (tiles[x + 1, y] == (int)TileType.Empty) && (tiles[x, y + 1] == (int)TileType.Empty))
                    {
                        if (TileIsWall(tiles[x, y]))
                            tiles[x, y] = (int)TileType.WallNO;
                        else if (tiles[x, y] == (int)TileType.Empty)
                        {
                            tiles[x - 1, y - 1] = (int)TileType.WallSE;
                            tiles[x - 1, y] = (int)TileType.WallNS;
                            tiles[x, y - 1] = (int)TileType.WallEO;                            
                        }
                    }

                    if (TileIsWall(tiles[x + 1, y]) && TileIsWall(tiles[x, y - 1]) && 
                       (tiles[x - 1, y] == (int)TileType.Empty) && (tiles[x, y + 1] == (int)TileType.Empty))
                    {
                        if (TileIsWall(tiles[x, y]))
                            tiles[x, y] = (int)TileType.WallNE;
                        else if (tiles[x, y] == (int)TileType.Empty)
                        {
                            tiles[x + 1, y - 1] = (int)TileType.WallSO;
                            tiles[x + 1, y] = (int)TileType.WallNS;
                            tiles[x, y - 1] = (int)TileType.WallEO; 
                        }
                    }

                    if (TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x, y + 1]) &&
                        (tiles[x + 1, y] == (int)TileType.Empty) && (tiles[x, y - 1] == (int)TileType.Empty))
                    {
                        if (TileIsWall(tiles[x, y]))
                            tiles[x, y] = (int)TileType.WallSO;
                        else if (tiles[x, y] == (int)TileType.Empty)
                        {
                            tiles[x - 1, y + 1] = (int)TileType.WallNE;
                            tiles[x - 1, y] = (int)TileType.WallNS;
                            tiles[x, y + 1] = (int)TileType.WallEO;
                        }
                    }

                    if (TileIsWall(tiles[x + 1, y]) && TileIsWall(tiles[x, y + 1]) && 
                       (tiles[x - 1, y] == (int)TileType.Empty) && (tiles[x, y - 1] == (int)TileType.Empty))
                    {
                        if (TileIsWall(tiles[x, y]))
                            tiles[x, y] = (int)TileType.WallSE;
                        else if (tiles[x, y] == (int)TileType.Empty)
                        {
                            tiles[x + 1, y + 1] = (int)TileType.WallNO;
                            tiles[x + 1, y] = (int)TileType.WallNS;
                            tiles[x, y + 1] = (int)TileType.WallEO;
                        }
                    }

                    if ((TileIsWall(tiles[x - 1, y - 1]) && TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x - 1, y + 1]) &&
                         TileIsWall(tiles[x, y - 1]) && TileIsWall(tiles[x, y + 1]) &&
                         (tiles[x + 1, y - 1] == (int)TileType.Empty) && (tiles[x + 1, y] == (int)TileType.Empty) && (tiles[x + 1, y + 1] == (int)TileType.Empty)) ||
                        (TileIsWall(tiles[x + 1, y - 1]) && TileIsWall(tiles[x + 1, y]) && TileIsWall(tiles[x + 1, y + 1]) &&
                         TileIsWall(tiles[x, y - 1]) && TileIsWall(tiles[x, y + 1]) &&
                         (tiles[x - 1, y - 1] == (int)TileType.Empty) && (tiles[x - 1, y] == (int)TileType.Empty) && (tiles[x - 1, y + 1] == (int)TileType.Empty)))
                    {
                        tiles[x, y] = (int)TileType.WallNS;                  
                    }
                    else if ((TileIsWall(tiles[x - 1, y - 1]) && TileIsWall(tiles[x, y - 1]) && TileIsWall(tiles[x + 1, y - 1]) &&
                         TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x + 1, y]) &&
                         (tiles[x - 1, y + 1] == (int)TileType.Empty) && (tiles[x, y + 1] == (int)TileType.Empty) && (tiles[x + 1, y + 1] == (int)TileType.Empty)) ||
                        (TileIsWall(tiles[x - 1, y + 1]) && TileIsWall(tiles[x, y + 1]) && TileIsWall(tiles[x + 1, y + 1]) &&
                         TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x + 1, y]) &&
                         (tiles[x - 1, y - 1] == (int)TileType.Empty) && (tiles[x, y - 1] == (int)TileType.Empty) && (tiles[x + 1, y - 1] == (int)TileType.Empty)))
                    {
                        tiles[x, y] = (int)TileType.WallEO;                    
                    }
                }
            }

            // mark three-way walls
            for (int x = 1; x < w - 1; x++)
            {
                for (int y = 1; y < h - 1; y++)
                {                    
                    if ( TileIsWall(tiles[x - 1, y - 1]) && TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x - 1, y + 1]) &&
                         TileIsWall(tiles[x, y - 1]) && TileIsWall(tiles[x, y]) && TileIsWall(tiles[x, y + 1]) &&
                         (tiles[x + 1, y - 1] == (int)TileType.Empty) && TileIsWall(tiles[x + 1, y]) &&  (tiles[x + 1, y + 1] == (int)TileType.Empty))
                    {
                        tiles[x, y] = (int)TileType.WallNES;            
                    }
                    else if (TileIsWall(tiles[x + 1, y - 1]) && TileIsWall(tiles[x + 1, y]) && TileIsWall(tiles[x + 1, y + 1]) &&
                         TileIsWall(tiles[x, y - 1]) && TileIsWall(tiles[x, y]) && TileIsWall(tiles[x, y + 1]) &&
                         (tiles[x - 1, y - 1] == (int)TileType.Empty) && TileIsWall(tiles[x - 1, y]) && (tiles[x - 1, y + 1] == (int)TileType.Empty))
                    {
                        tiles[x, y] = (int)TileType.WallNSO;
                    }
                    else if (TileIsWall(tiles[x - 1, y - 1]) && TileIsWall(tiles[x, y - 1]) && TileIsWall(tiles[x + 1, y - 1]) &&
                        TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x, y]) && TileIsWall(tiles[x + 1, y]) &&
                         (tiles[x - 1, y + 1] == (int)TileType.Empty) && TileIsWall(tiles[x, y + 1]) && (tiles[x + 1, y + 1] == (int)TileType.Empty))
                    {
                        tiles[x, y] = (int)TileType.WallESO;
                    }
                    else if(TileIsWall(tiles[x - 1, y + 1]) && TileIsWall(tiles[x, y + 1]) && TileIsWall(tiles[x + 1, y + 1]) &&
                        TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x, y]) && TileIsWall(tiles[x + 1, y]) &&
                         (tiles[x - 1, y - 1] == (int)TileType.Empty) && TileIsWall(tiles[x, y - 1]) && (tiles[x + 1, y - 1] == (int)TileType.Empty))
                    {
                        tiles[x, y] = (int)TileType.WallNEO;
                    }

                    else if (TileIsWall(tiles[x, y - 1]) &&
                            ((tiles[x - 1, y - 1] == (int)TileType.Empty) && TileIsWall(tiles[x + 1, y - 1]) || (tiles[x + 1, y - 1] == (int)TileType.Empty) && TileIsWall(tiles[x - 1, y - 1]) ) &&
                             TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x, y]) && TileIsWall(tiles[x + 1, y]) &&
                             (tiles[x - 1, y + 1] == (int)TileType.Empty) && (tiles[x, y + 1] == (int)TileType.Empty) && (tiles[x + 1, y + 1] == (int)TileType.Empty))
                    {
                        tiles[x, y] = (int)TileType.WallNEO;
                    }

                    else if( (tiles[x - 1, y - 1] == (int)TileType.Empty) && TileIsWall(tiles[x, y - 1]) &&  (tiles[x + 1, y - 1] == (int)TileType.Empty) && 
                             TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x, y]) && (tiles[x + 1, y] == (int)TileType.Empty) &&
                             TileIsWall(tiles[x - 1, y + 1]) && TileIsWall(tiles[x, y + 1]) && (tiles[x + 1, y + 1] == (int)TileType.Empty))
                    {
                        tiles[x, y] = (int)TileType.WallNSO;
                    }
                    else if (TileIsWall(tiles[x - 1, y - 1]) && TileIsWall(tiles[x, y - 1]) && (tiles[x + 1, y - 1] == (int)TileType.Empty) &&
                         TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x, y]) && (tiles[x + 1, y] == (int)TileType.Empty) &&
                         (tiles[x - 1, y + 1] == (int)TileType.Empty) && TileIsWall(tiles[x, y + 1]) && (tiles[x + 1, y + 1] == (int)TileType.Empty))
                    {
                        tiles[x, y] = (int)TileType.WallNSO;
                    }      

                    else if ( ( TileIsWall(tiles[x - 1, y - 1]) || (tiles[x - 1, y - 1] == (int)TileType.Empty) ) && 
                            TileIsWall(tiles[x, y - 1]) && (tiles[x + 1, y - 1] == (int)TileType.Empty) &&
                            TileIsWall(tiles[x - 1, y]) && TileIsWall(tiles[x, y]) && TileIsWall(tiles[x + 1, y]) &&
                            (tiles[x - 1, y + 1] == (int)TileType.Empty) && TileIsWall(tiles[x, y + 1]) && (tiles[x + 1, y + 1] == (int)TileType.Empty))
                    {
                        tiles[x, y] = (int)TileType.WallNESO;
                    }
                }
            }

            // remove the doors in the middle of a room
            /*  for (int x = 0; x < w; x++)
              {
                  for (int y = 0; y < h; y++)
                  {
                      if (tiles[x, y] != (int)TileType.Door) continue;

                      if (tiles[x - 1, y] == (int)TileType.Wall) continue;
                      if (tiles[x, y - 1] == (int)TileType.Wall) continue;
                      if (tiles[x - 1, y - 1] == (int)TileType.Wall) continue;

                      if (tiles[x + 1, y] == (int)TileType.Wall) continue;
                      if (tiles[x, y + 1] == (int)TileType.Wall) continue;
                      if (tiles[x + 1, y + 1] == (int)TileType.Wall) continue;

                      if (tiles[x - 1, y + 1] == (int)TileType.Wall) continue;
                      if (tiles[x + 1, y - 1] == (int)TileType.Wall) continue;

                      tiles[x, y] = (int)TileType.Empty;
                  }
              }
              // clean up doors glitches
              for (int x = 0; x < w; x++)
              {
                  for (int y = 0; y < h; y++)
                  {
                      if (tiles[x, y] != (int)TileType.Door) continue;

                      if (tiles[x - 1, y] == (int)TileType.Wall && tiles[x + 1, y] == (int)TileType.Empty)
                      {
                          tiles[x, y] = (int)TileType.Empty;
                          continue;
                      }
                      if (tiles[x + 1, y] == (int)TileType.Wall && tiles[x - 1, y] == (int)TileType.Empty)
                      {
                          tiles[x, y] = (int)TileType.Empty;
                          continue;
                      }
                      if (tiles[x, y - 1] == (int)TileType.Wall && tiles[x, y + 1] == (int)TileType.Empty)
                      {
                          tiles[x, y] = (int)TileType.Empty;
                          continue;
                      }
                      if (tiles[x, y + 1] == (int)TileType.Wall && tiles[x, y - 1] == (int)TileType.Empty)
                      {
                          tiles[x, y] = (int)TileType.Empty;
                          continue;
                      }
                  }
              }*/

            return tiles;
        }

        private static bool TileIsWall(int tile)
        {
            return (tile >= (int)TileType.Wall && tile <= (int)TileType.WallNESO) || tile == (int)TileType.Void;
        }

        #endregion

        #region Properties

        public int Width
        {
            get { return width; }
            set { width = value; }
        }

        public int Height
        {
            get { return height; }
            set { height = value; }
        }

        public int ChangeDirectionModifier
        {
            get { return changeDirectionModifier; }
            set { changeDirectionModifier = value; }
        }

        public int SparsenessModifier
        {
            get { return sparsenessModifier; }
            set { sparsenessModifier = value; }
        }

        public int DeadEndRemovalModifier
        {
            get { return deadEndRemovalModifier; }
            set { deadEndRemovalModifier = value; }
        }

        #endregion

    }
}