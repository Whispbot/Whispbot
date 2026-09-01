using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Logging
{
    public static class LogStyles
    {
        public static string Get(params int[] colours)
        {
            return $"\u001b[{string.Join(";", colours)}m";
        }

        public static string Reset => Get(0);

        public static string Black => Get(30);
        public static string Red => Get(31);
        public static string Green => Get(32);
        public static string Yellow => Get(33);
        public static string Blue => Get(34);
        public static string Magenta => Get(35);
        public static string Cyan => Get(36);
        public static string LightGray => Get(37);
        public static string DarkGray => Get(90);
        public static string LightRed => Get(91);
        public static string LightGreen => Get(92);
        public static string LightYellow => Get(93);
        public static string LightBlue => Get(94);
        public static string LightMagenta => Get(95);
        public static string LightCyan => Get(96);
        public static string White => Get(97);

        public static string Bold => Get(1);
        public static string Normal => Get(22);
        public static string Underline => Get(4);
        public static string NoUnderline => Get(24);
        public static string Reverse => Get(7);
        public static string NoReverse => Get(27);
    }
}
