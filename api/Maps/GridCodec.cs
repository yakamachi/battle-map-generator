namespace BattleMapGenerator.Api.Maps;

// How a saved map's grid is stored in SavedMaps: cells as one byte per cell (row-major, the CellKind
// number) and rooms as 5 bytes each (X, Y, Width, Height, the RoomKind number). The enum numbers are
// therefore persisted, which is why CellKind and RoomKind may only grow by appending.
// Decoding checks what it reads instead of trusting the row, so a corrupted or outdated row fails
// loudly with InvalidDataException rather than producing a grid web/ cannot draw.
public static class GridCodec
{
    public const int BytesPerRoom = 5;

    // Every room coordinate and side fits in one byte because a map is at most 60×60.
    private const int MaxRoomValue = MapSize.MaxWidth > MapSize.MaxHeight ? MapSize.MaxWidth : MapSize.MaxHeight;

    public static byte[] EncodeCells(CellKind[] cells)
    {
        var bytes = new byte[cells.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            if (!Enum.IsDefined(cells[i]))
            {
                throw new InvalidDataException($"Cell {i} has the undefined kind {(int)cells[i]}.");
            }
            bytes[i] = (byte)cells[i];
        }
        return bytes;
    }

    public static CellKind[] DecodeCells(byte[] bytes, int width, int height)
    {
        if (width < 0 || height < 0 || bytes.Length != width * height)
        {
            throw new InvalidDataException($"Expected {width}×{height} cells, found {bytes.Length} bytes.");
        }

        var cells = new CellKind[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            var kind = (CellKind)bytes[i];
            if (!Enum.IsDefined(kind))
            {
                throw new InvalidDataException($"Cell {i} has the undefined kind {bytes[i]}.");
            }
            cells[i] = kind;
        }
        return cells;
    }

    public static byte[] EncodeRooms(Room[] rooms)
    {
        var bytes = new byte[rooms.Length * BytesPerRoom];
        for (var i = 0; i < rooms.Length; i++)
        {
            var room = rooms[i];
            if (!Enum.IsDefined(room.Kind))
            {
                throw new InvalidDataException($"Room {i} has the undefined kind {(int)room.Kind}.");
            }

            var offset = i * BytesPerRoom;
            bytes[offset] = RoomByte(room.X, i);
            bytes[offset + 1] = RoomByte(room.Y, i);
            bytes[offset + 2] = RoomByte(room.Width, i);
            bytes[offset + 3] = RoomByte(room.Height, i);
            bytes[offset + 4] = (byte)room.Kind;
        }
        return bytes;
    }

    public static Room[] DecodeRooms(byte[] bytes)
    {
        if (bytes.Length % BytesPerRoom != 0)
        {
            throw new InvalidDataException($"Room bytes must come in groups of {BytesPerRoom}, found {bytes.Length}.");
        }

        var rooms = new Room[bytes.Length / BytesPerRoom];
        for (var i = 0; i < rooms.Length; i++)
        {
            var offset = i * BytesPerRoom;
            for (var field = 0; field < BytesPerRoom - 1; field++)
            {
                if (bytes[offset + field] > MaxRoomValue)
                {
                    throw new InvalidDataException($"Room {i} has the value {bytes[offset + field]}, above {MaxRoomValue}.");
                }
            }

            var kind = (RoomKind)bytes[offset + 4];
            if (!Enum.IsDefined(kind))
            {
                throw new InvalidDataException($"Room {i} has the undefined kind {bytes[offset + 4]}.");
            }
            rooms[i] = new Room(bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3], kind);
        }
        return rooms;
    }

    private static byte RoomByte(int value, int room) =>
        value is >= 0 and <= MaxRoomValue
            ? (byte)value
            : throw new InvalidDataException($"Room {room} has the value {value}, outside 0 to {MaxRoomValue}.");
}
