using System.Net.Http.Headers;

namespace Stargate.Helpers;

/// <summary>
/// Centralized place for parsing/normalizing Mime types and charsets
/// </summary>
public static class MimeHelper
{
    public static string? NormalizeCharset(MediaTypeHeaderValue? contentType)
    {
        string charset = contentType?.CharSet ?? "";
        // Trim whitespace and quotes some servers add around charset, which makes .NET's Encoding.GetEncoding throw an exception
        charset = charset.Trim().Trim('"', '\'');
        
        return !string.IsNullOrWhiteSpace(charset) ? charset : null; 
    }
}