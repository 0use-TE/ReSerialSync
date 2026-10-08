using System;
using System.Linq;
using System.Text;

namespace ReSerialSync.Services;

public sealed class SerialInputException(string resourceKey) : FormatException(resourceKey)
{
    public string ResourceKey { get; } = resourceKey;
}

public static class SerialCodec
{
    public static Encoding GetEncoding(string name) => name switch
    {
        "ASCII" => Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback),
        "Latin-1" => Encoding.Latin1,
        _ => new UTF8Encoding(false, true)
    };

    public static byte[] Encode(string text, bool hex, string encoding, string lineEnding)
    {
        byte[] payload;
        if (hex)
        {
            var compact = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (compact.Length % 2 != 0) throw new SerialInputException("HexLength");
            try { payload = Convert.FromHexString(compact); }
            catch (FormatException) { throw new SerialInputException("HexCharacters"); }
        }
        else payload = GetEncoding(encoding).GetBytes(text);
        var ending = lineEnding switch { "CR" => "\r", "LF" => "\n", "CRLF" => "\r\n", _ => "" };
        return [.. payload, .. Encoding.ASCII.GetBytes(ending)];
    }

    public static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace('-', ' ');
    public static Decoder CreateDecoder(string name) =>
        (name == "UTF-8" ? new UTF8Encoding(false, false) : GetEncoding(name)).GetDecoder();
}
