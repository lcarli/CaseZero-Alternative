using System.Text.Json;

namespace CaseZeroApi.Services
{
    #region Utils
    public static class CaseUtils
    {
        public static string? ExtractCaseId(string caseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(caseJson);
                if (doc.RootElement.TryGetProperty("caseId", out var id))
                    return id.GetString();
            }
            catch { /* ignore */ }
            return null;
        }

        public static List<string> ExtractIds(string caseJson, string arrayName)
        {
            var list = new List<string>();
            try
            {
                using var doc = JsonDocument.Parse(caseJson);
                if (doc.RootElement.TryGetProperty(arrayName, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        if (el.TryGetProperty("id", out var id))
                        {
                            var v = id.GetString();
                            if (!string.IsNullOrWhiteSpace(v)) list.Add(v!);
                        }
                    }
                }
            }
            catch { /* ignore */ }
            return list;
        }
    }
    #endregion
}