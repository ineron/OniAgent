using System.Collections.Generic;

namespace OniAgent.Commands
{
    // One instruction in a batch. Not every field applies to every Type —
    // "dig_rect" uses X1/X2/Y1/Y2, "build" uses Building/X/Y, "set_paused"
    // uses Paused. All coordinates are cell offsets relative to the
    // batch's origin (the Duplicant Printing Pod), not absolute world
    // cells — the caller doesn't know (and shouldn't need to know)
    // absolute grid coordinates.
    public class CommandItem
    {
        public string Type;

        public int X1;
        public int X2;
        public int Y1;
        public int Y2;

        public string Building;
        public int X;
        public int Y;

        public bool Paused;
    }

    public class CommandBatchRequest
    {
        public List<CommandItem> Commands = new List<CommandItem>();
    }

    public class CommandItemResult
    {
        public string Type;
        public bool Ok;
        public string Message;

        public static CommandItemResult Success(CommandItem item, string message)
        {
            return new CommandItemResult { Type = item.Type, Ok = true, Message = message };
        }

        public static CommandItemResult Fail(CommandItem item, string message)
        {
            return new CommandItemResult { Type = item?.Type, Ok = false, Message = message };
        }
    }

    public class CommandBatchResult
    {
        public string BatchId;
        public string ExecutedAtUtc;
        public List<CommandItemResult> Results = new List<CommandItemResult>();
    }
}
