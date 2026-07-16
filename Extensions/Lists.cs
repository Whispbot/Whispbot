using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Extensions
{
    public static class Lists
    {
        public static string Join(this IEnumerable<object> list, string seperator, string? lastSeperator = null)
        {
            if (lastSeperator == null)
                return String.Join(seperator, list);

            var items = new List<object>(list);
            if (items.Count == 0)
                return string.Empty;

            if (items.Count == 1)
                return items[0].ToString()!;

            var lastItem = items[^1];
            items.RemoveAt(items.Count - 1);

            return String.Join(seperator, items) + lastSeperator + lastItem;
        }
    }
}
