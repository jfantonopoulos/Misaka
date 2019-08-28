using System;
using System.Collections.Generic;
using System.Text;

namespace Misaka.Extensions
{
    public static class Markdown
    {
        private static string Cleanse(string str)
        {
            return str.Replace("`", "'");
        }

        public static string Bold(this String str)
        {
            return Cleanse($"**{str}**");
        }

        public static string Italics(this String str)
        {
            return Cleanse($"*{str}*");
        }

        public static string Underline(this String str)
        {
            return Cleanse($"__{str}__");
        }

        public static string Strikeout(this String str)
        {
            return Cleanse($"~~{str}~~");
        }

        public static string Code(this String str)
        {
            return $"`{Cleanse(str)}`";
        }

        public static string CodeBlock(this String str, string lang = null)
        {
            if (lang != null)
                return $"```{lang}\n{Cleanse(str)}```";
            else
                return $"```{Cleanse(str)}```";
        }
    }
}
