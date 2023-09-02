using System.ComponentModel.Design;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace PandaHunt.PuzzleDData
{
    public class MineField
    {
        public bool RedLight { get; set; }
        public bool GreenLight { get; set; }
        public bool BlueLight { get; set; }


        private bool[,] redMines = new bool[25, 25];
        private bool[,] blueMines = new bool[25, 25];
        private bool[,] greenMines = new bool[25, 25];
        private bool[,] yellowMines = new bool[25, 25];
        private bool[,] cyaneMines = new bool[25, 25];
        private bool[,] magentaMines = new bool[25, 25];
        private bool[,] whiteMines = new bool[25, 25];

        private int[] pandaPosition = { 12, 12 };

        public MineField()
        {
            RedLight = false;
            GreenLight = false;
            BlueLight = false;
            GenerateMinefield();
        }


        public void MoveUp()
        {
            Move(1);
        }
        public void MoveDown()
        {
            Move(2);
        }
        public void MoveLeft()
        {
            Move(3);
        }
        public void MoveRight()
        {
            Move(4);
        }

        public void Move(int direction)
        {
            int[] newPosition = ChangePositionInDirection(pandaPosition, direction);
            if (CheckIfOutOfMinefield(newPosition)) throw new OutOfMinefieldException();
            if (CheckForMine(newPosition[0], newPosition[1])) throw new SteppedOnMineException();
            pandaPosition = newPosition;
        }

        public void Reset()
        {
            pandaPosition = new int[] { 12, 12 };
            GenerateMinefield();
        }

        private bool CheckForMine(int x, int y)
        {
            return redMines[x, y] || greenMines[x, y] || blueMines[x, y] || yellowMines[x, y] || cyaneMines[x, y] || magentaMines[x, y] || whiteMines[x, y];
        }
        private bool CheckIfOutOfMinefield(int[] position)
        {
            return (position[0] > 24 || position[0] < 0 || position[1] > 24 || position[1] < 0);
        }

        private void GenerateMinefield()
        {
            Random rnd = new Random();
            for (int i = 0; i < 25; i++)
            {
                for (int j = 0; j < 25; j++)
                {
                    int color = rnd.Next(1, 8);

                    switch (color)
                    {
                        case 1:
                            redMines[i, j] = true;
                            break;
                        case 2:
                            blueMines[i, j] = true;
                            break;
                        case 3:
                            greenMines[i, j] = true;
                            break;
                        case 4:
                            yellowMines[i, j] = true;
                            break;
                        case 5:
                            magentaMines[i, j] = true;
                            break;
                        case 6:
                            cyaneMines[i, j] = true;
                            break;
                        case 7:
                            whiteMines[i, j] = true;
                            break;
                    }

                }
            }
            ClearPath(true,1);
            ClearPath(false,2);
            ClearPath(false,3);
        }

        private void ClearPath(bool first, int preferedDirection = 0)
        {
            int[] position = new int[] { 12, 12 };
            if (first)
            {
                ClearPosition(position[0], position[1]);
            }
            int lastDirection = 0;
            int sameDirectionCounter = 0;
            Random rnd = new Random();

            while (true)
            {
                int direction;

                if (preferedDirection != 0)
                {
                    direction = preferedDirection;
                    preferedDirection = 0;
                }
                else
                {
                    direction = PreventReversing(rnd.Next(1, 5),lastDirection);
                }
                
                if (direction == lastDirection)
                {
                    sameDirectionCounter += 1;
                    if (sameDirectionCounter >= 4)
                    {
                        while (direction == lastDirection) direction = PreventReversing(rnd.Next(1, 5),lastDirection);
                        sameDirectionCounter = 0;
                    }
                }
                
                int[] newPosition = ChangePositionInDirection(position, direction);
                if (position == newPosition) continue;
                //if (!CheckForMine(newPosition[0], newPosition[1])) continue;
                ClearPosition(newPosition[0], newPosition[1]);
                if (IsNeighbourOutOfMinefield(newPosition)) break;
                position = newPosition;
                lastDirection = direction;
            }
        }

        private int PreventReversing(int direction, int lastDirection)
        {
            if (lastDirection == 1 && direction == 2) return 1;
            if (lastDirection == 2 && direction == 1) return 2;
            if (lastDirection == 3 && direction == 4) return 3;
            if (lastDirection == 4 && direction == 3) return 4;
            return direction;
        }


        private bool IsNeighbourOutOfMinefield(int[] position)
        {
            if (CheckIfOutOfMinefield(ChangePositionInDirection(position, 1))) return true;
            if (CheckIfOutOfMinefield(ChangePositionInDirection(position, 2))) return true;
            if (CheckIfOutOfMinefield(ChangePositionInDirection(position, 3))) return true;
            if (CheckIfOutOfMinefield(ChangePositionInDirection(position, 4))) return true;
            return false;
        }


        private int[] ChangePositionInDirection(int[] position, int direction)
        {
            // 1 up
            // 2 down
            // 3 left
            // 4 right
            switch (direction)
            {
                case 1:
                    return new int[] { position[0], position[1] - 1 };
                case 2:
                    return new int[] { position[0], position[1] + 1 };
                case 3:
                    return new int[] { position[0] - 1, position[1] };
                case 4:
                    return new int[] { position[0] + 1, position[1] };
            }
            return position;
        }

        private void ClearPosition(int x, int y)
        {
            redMines[x, y] = false;
            blueMines[x, y] = false;
            greenMines[x, y] = false;
            yellowMines[x, y] = false;
            magentaMines[x, y] = false;
            cyaneMines[x, y] = false;
            whiteMines[x, y] = false;
        }


        public string GetPositionHtml(int x, int y)
        {
            if (x == pandaPosition[0] && y == pandaPosition[1]) return ReadSvg("panda-tile.svg");
            if (CheckForMine(x, y)) return GetMineHtml(x,y);
            //if (CheckForMine(x, y)) return "<span class=\"" + GetMineColor(x, y) + "\">*</span>";
            return ReadSvg("clear-tile.svg");
        }

        private string GetMineHtml(int x, int y)
        {
            if ((RedLight && GreenLight && BlueLight) && whiteMines[x,y] ) return ReadSvg("white-mine-tile.svg");
            if ((RedLight && GreenLight && !BlueLight ) && yellowMines[x,y]) return "<div class=\"mine-yellow\">" + ReadSvg("white-mine-tile.svg") + "</div>";
            if ((RedLight && !GreenLight && BlueLight) && magentaMines[x,y]) return "<div class=\"mine-magenta\">" + ReadSvg("white-mine-tile.svg") + "</div>";
            if ((!RedLight && GreenLight && BlueLight) && cyaneMines[x,y]) return "<div class=\"mine-cyan\">" + ReadSvg("white-mine-tile.svg") + "</div>";
            if ((RedLight && !GreenLight && !BlueLight) && redMines[x,y]) return "<div class=\"mine-red\">" + ReadSvg("white-mine-tile.svg") + "</div>";
            if ((!RedLight && !GreenLight && BlueLight) && blueMines[x,y]) return "<div class=\"mine-blue\">" + ReadSvg("white-mine-tile.svg") + "</div>";
            if ((!RedLight && GreenLight && !BlueLight) && greenMines[x,y]) return "<div class=\"mine-green\">" + ReadSvg("white-mine-tile.svg") + "</div>";
            return ReadSvg("clear-tile.svg");

        }

        private string GetMineColor(int x, int y)
        {
            if (redMines[x, y]) return "red-mine";
            if (blueMines[x, y]) return "blue-mine";
            if (greenMines[x, y]) return "green-mine";
            if (yellowMines[x, y]) return "yellow-mine";
            if (magentaMines[x, y]) return "magenta-mine";
            if (cyaneMines[x, y]) return "cyane-mine";
            if (whiteMines[x, y]) return "white-mine";
            return "no-mine";
        }

        private string ReadSvg(string filename)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "PandaHunt.PuzzleDData." + filename;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
        [Serializable]
        internal class SteppedOnMineException : Exception
        {
            public SteppedOnMineException()
            {
            }

            public SteppedOnMineException(string? message) : base(message)
            {
            }

            public SteppedOnMineException(string? message, Exception? innerException) : base(message, innerException)
            {
            }

            protected SteppedOnMineException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }

        [Serializable]
        internal class OutOfMinefieldException : Exception
        {
            public OutOfMinefieldException()
            {
            }

            public OutOfMinefieldException(string? message) : base(message)
            {
            }

            public OutOfMinefieldException(string? message, Exception? innerException) : base(message, innerException)
            {
            }

            protected OutOfMinefieldException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }
}
