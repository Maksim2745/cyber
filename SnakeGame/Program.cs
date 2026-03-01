using System.Text;

if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.WriteLine("Для запуска игры нужна интерактивная консоль.");
    return;
}

var game = new SnakeGame(width: 30, height: 20, initialTickDelayMs: 140);
game.Run();

internal sealed class SnakeGame
{
    private readonly int _width;
    private readonly int _height;
    private readonly int _initialTickDelayMs;
    private readonly Random _random = new();

    private readonly LinkedList<Cell> _snake = new();
    private readonly HashSet<Cell> _occupied = new();

    private Direction _direction;
    private Direction _nextDirection;
    private Cell _food;

    private bool _gameOver;
    private bool _won;
    private int _score;
    private int _tickDelayMs;

    public SnakeGame(int width, int height, int initialTickDelayMs)
    {
        if (width < 10 || height < 10)
        {
            throw new ArgumentException("Поле должно быть не меньше 10x10.");
        }

        _width = width;
        _height = height;
        _initialTickDelayMs = initialTickDelayMs;
    }

    public void Run()
    {
        var requiredConsoleWidth = _width + 2;
        var requiredConsoleHeight = _height + 5;

        if (Console.WindowWidth < requiredConsoleWidth || Console.WindowHeight < requiredConsoleHeight)
        {
            Console.WriteLine($"Увеличьте окно консоли минимум до {requiredConsoleWidth}x{requiredConsoleHeight} и запустите игру снова.");
            return;
        }

        Console.CursorVisible = false;
        Console.Clear();

        try
        {
            var running = true;
            while (running)
            {
                ResetState();
                Render();

                while (!_gameOver)
                {
                    HandleInput(ref running);
                    if (!running)
                    {
                        break;
                    }

                    Tick();
                    Render();
                    Thread.Sleep(_tickDelayMs);
                }

                if (!running)
                {
                    break;
                }

                running = ShowEndScreenAndAskRestart();
                Console.Clear();
            }
        }
        finally
        {
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, _height + 4);
        }
    }

    private void ResetState()
    {
        _snake.Clear();
        _occupied.Clear();
        _score = 0;
        _gameOver = false;
        _won = false;
        _tickDelayMs = _initialTickDelayMs;

        var centerY = _height / 2;
        var centerX = _width / 2;

        // Стартовая змейка из 3 сегментов, движется вправо.
        AddSnakePart(new Cell(centerX, centerY));
        AddSnakePart(new Cell(centerX - 1, centerY));
        AddSnakePart(new Cell(centerX - 2, centerY));

        _direction = Direction.Right;
        _nextDirection = Direction.Right;
        PlaceFood();
    }

    private void AddSnakePart(Cell part)
    {
        _snake.AddLast(part);
        _occupied.Add(part);
    }

    private void HandleInput(ref bool running)
    {
        while (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true).Key;

            if (key == ConsoleKey.Escape || key == ConsoleKey.Q)
            {
                running = false;
                return;
            }

            var newDirection = key switch
            {
                ConsoleKey.W or ConsoleKey.UpArrow => Direction.Up,
                ConsoleKey.S or ConsoleKey.DownArrow => Direction.Down,
                ConsoleKey.A or ConsoleKey.LeftArrow => Direction.Left,
                ConsoleKey.D or ConsoleKey.RightArrow => Direction.Right,
                _ => _nextDirection
            };

            if (!IsOpposite(newDirection, _nextDirection))
            {
                _nextDirection = newDirection;
            }
        }
    }

    private void Tick()
    {
        _direction = _nextDirection;
        var head = _snake.First!.Value;
        var nextHead = NextCell(head, _direction);

        if (IsOutsideField(nextHead))
        {
            _gameOver = true;
            return;
        }

        var tail = _snake.Last!.Value;
        var shouldGrow = nextHead.Equals(_food);

        // В клетку хвоста можно зайти только если в этом тике хвост сдвинется.
        var hitsBody = _occupied.Contains(nextHead) && !(nextHead.Equals(tail) && !shouldGrow);
        if (hitsBody)
        {
            _gameOver = true;
            return;
        }

        _snake.AddFirst(nextHead);
        _occupied.Add(nextHead);

        if (shouldGrow)
        {
            _score++;
            _tickDelayMs = Math.Max(60, _tickDelayMs - 3);

            if (_occupied.Count == _width * _height)
            {
                _won = true;
                _gameOver = true;
                return;
            }

            PlaceFood();
            return;
        }

        _snake.RemoveLast();
        _occupied.Remove(tail);
    }

    private Cell NextCell(Cell current, Direction direction) =>
        direction switch
        {
            Direction.Up => new Cell(current.X, current.Y - 1),
            Direction.Down => new Cell(current.X, current.Y + 1),
            Direction.Left => new Cell(current.X - 1, current.Y),
            Direction.Right => new Cell(current.X + 1, current.Y),
            _ => current
        };

    private bool IsOutsideField(Cell cell) =>
        cell.X < 0 || cell.X >= _width || cell.Y < 0 || cell.Y >= _height;

    private void PlaceFood()
    {
        Cell candidate;
        do
        {
            candidate = new Cell(
                X: _random.Next(0, _width),
                Y: _random.Next(0, _height));
        } while (_occupied.Contains(candidate));

        _food = candidate;
    }

    private void Render()
    {
        var buffer = new StringBuilder((_width + 4) * (_height + 4));
        var head = _snake.First!.Value;

        buffer.Append('+').Append(new string('-', _width)).AppendLine("+");
        for (var y = 0; y < _height; y++)
        {
            buffer.Append('|');
            for (var x = 0; x < _width; x++)
            {
                var cell = new Cell(x, y);
                var symbol = cell.Equals(head)
                    ? '@'
                    : cell.Equals(_food)
                        ? '*'
                        : _occupied.Contains(cell)
                            ? 'o'
                            : ' ';

                buffer.Append(symbol);
            }

            buffer.AppendLine("|");
        }

        buffer.Append('+').Append(new string('-', _width)).AppendLine("+");
        buffer.Append("Счёт: ")
            .Append(_score)
            .Append("  Управление: WASD/стрелки, Q или Esc - выход");

        Console.SetCursorPosition(0, 0);
        Console.Write(buffer.ToString().PadRight(Console.WindowWidth * Console.WindowHeight));
    }

    private bool ShowEndScreenAndAskRestart()
    {
        Console.SetCursorPosition(0, _height + 3);

        var text = _won
            ? $"Победа! Финальный счёт: {_score}. Нажми R для новой игры или Q/Esc для выхода."
            : $"Игра окончена. Счёт: {_score}. Нажми R для новой игры или Q/Esc для выхода.";

        Console.Write(text.PadRight(Math.Max(text.Length, Console.WindowWidth - 1)));

        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.R or ConsoleKey.Enter)
            {
                return true;
            }

            if (key is ConsoleKey.Q or ConsoleKey.Escape)
            {
                return false;
            }
        }
    }

    private static bool IsOpposite(Direction a, Direction b) =>
        (a == Direction.Left && b == Direction.Right)
        || (a == Direction.Right && b == Direction.Left)
        || (a == Direction.Up && b == Direction.Down)
        || (a == Direction.Down && b == Direction.Up);
}

internal readonly record struct Cell(int X, int Y);

internal enum Direction
{
    Up,
    Down,
    Left,
    Right
}
