using System;

// Paging is independent of asset names and the number of levels in a biome.
public static class SelectionRouteLayout
{
    public static int Columns(float width) => width < 1100f ? 3 : 4;
    public static int PageCount(int count, int pageSize) => Math.Max(1, (Math.Max(0, count) + pageSize - 1) / pageSize);
    public static int PageForIndex(int index, int pageSize) => Math.Max(0, index) / pageSize;
    public static int VisibleCount(int count, int page, int pageSize) => Math.Max(0, Math.Min(pageSize, count - page * pageSize));
    public static void Position(int slot, int columns, out float x, out float y, int seed = 0, int count = 0)
    {
        int rows = count > 0 ? Math.Min(3,(count + columns - 1)/columns) : 3;
        int row=0, col=slot, rowColumns=columns;
        if(count > 0)
        {
            int baseCount=count/rows, extra=count%rows;
            rowColumns=baseCount+(row<extra ? 1 : 0);
            while(col>=rowColumns && row<rows-1)
            {
                col-=rowColumns; row++;
                rowColumns=baseCount+(row<extra ? 1 : 0);
            }
        }
        else { row=slot/columns; col=slot%columns; }
        if (row % 2 == 1) col = rowColumns - 1 - col;
        var random = new Random(unchecked(seed * 397 ^ slot * 7919));
        x = (col + .5f + (float)(random.NextDouble()-.5)*.22f) / rowColumns;
        y = (rows == 1 ? .5f : .84f - row * .64f/(rows-1)) + (float)(random.NextDouble()-.5)*.06f;
    }
}
