using Vecxy.Pathfinding;
using Vecxy.UI;

namespace Pathfinding.Tests;

public sealed class WorldGrid : AUiComponent
{
    private readonly List<UiPanel> _cells = [];

    public WorldGrid(UiDocument document, int columns, int rows) : base(document.CreatePanel())
    {
        Columns = columns;
        Rows = rows;
        Root.SetAttribute("class", "world-grid");
        Root.Style.Set("grid-template-columns", $"repeat({columns}, 1fr)");
        Root.Style.Set("grid-template-rows", $"repeat({rows}, 1fr)");

        for (var y = 0; y < rows; y++)
        for (var x = 0; x < columns; x++)
        {
            var position = new GridPoint(x, y);
            var cell = document.CreatePanel(new Dictionary<string, string> { ["class"] = "world-cell" });
            cell.Clicked += _ => CellClicked?.Invoke(position);
            Root.Add(cell);
            _cells.Add(cell);
        }
    }

    public int Columns { get; }
    public int Rows { get; }
    public event Action<GridPoint>? CellClicked;

    public UiPanel GetCell(GridPoint position) => _cells[position.Y * Columns + position.X];
}
