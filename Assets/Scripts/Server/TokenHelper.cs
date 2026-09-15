public static class TokenHelper
{
    public static string ExtractPlayerId(string token)
    {
        try
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3) return string.Empty;

            string payload = parts[1];
            int mod = payload.Length % 4;
            if (mod > 0) payload += new string('=', 4 - mod);

            string json = System.Text.Encoding.UTF8.GetString(
                System.Convert.FromBase64String(payload));

            int idx = json.IndexOf("\"PlayerId\":\"");
            if (idx < 0) return string.Empty;
            int start = idx + 12;
            int end = json.IndexOf("\"", start);
            return json.Substring(start, end - start);
        }
        catch
        {
            return string.Empty;
        }
    }
}