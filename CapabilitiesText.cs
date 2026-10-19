using System;
using System.Text;

namespace Screenctl;

internal static class CapabilitiesText
{
    public static string Format(string text)
    {
        text = text.Trim();
        var output = new StringBuilder();
        var depth = 0;
        var position = 0;
        while (position < text.Length)
        {
            if (text[position] == ')')
            {
                depth--;
                Line(")");
                position++;
                continue;
            }

            var opening = text.IndexOf('(', position);
            var closing = text.IndexOf(')', position);
            if (opening < 0 || closing < opening)
            {
                Line(text[position..closing].Trim());
                position = closing;
                continue;
            }

            var prefix = text[position..opening].Trim();
            var separator = prefix.LastIndexOfAny([' ', '\r', '\n', '\t']);
            if (separator >= 0)
            {
                Line(prefix[..separator].TrimEnd());
                prefix = prefix[(separator + 1)..];
            }

            var end = text.IndexOf(')', opening + 1);
            var nested = text.IndexOf('(', opening + 1);
            if (nested < 0 || end < nested)
            {
                Line(prefix + text[opening..(end + 1)]);
                position = end + 1;
            }
            else
            {
                Line(prefix + "(");
                depth++;
                position = opening + 1;
            }
        }
        return output.ToString().TrimEnd();

        void Line(string value)
        {
            if (value.Length > 0) output.Append(' ', depth * 2).AppendLine(value);
        }
    }
}
