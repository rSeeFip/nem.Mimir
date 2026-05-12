using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace nem.Lume.McpTools.Tools;

[McpServerToolType]
public static class LumeScheduleEventTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "lume_schedule_event"), Description("Creates a calendar event in Lume.")]
    public static async Task<string> ScheduleEventAsync(
        [Description("Calendar identifier.")] string calendarId,
        [Description("Event title.")] string title,
        [Description("Start time in UTC ISO 8601 format.")] string startTimeUtc,
        [Description("End time in UTC ISO 8601 format.")] string endTimeUtc,
        [Description("Optional comma-separated attendee user IDs.")] string? attendees,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        if (!DateTimeOffset.TryParse(startTimeUtc, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var start))
        {
            return JsonSerializer.Serialize(new { error = "startTimeUtc is not a valid ISO 8601 date-time.", status = "error" }, JsonOptions);
        }

        if (!DateTimeOffset.TryParse(endTimeUtc, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var end))
        {
            return JsonSerializer.Serialize(new { error = "endTimeUtc is not a valid ISO 8601 date-time.", status = "error" }, JsonOptions);
        }

        if (start >= end)
        {
            return JsonSerializer.Serialize(new { error = "startTimeUtc must be before endTimeUtc.", status = "error" }, JsonOptions);
        }

        try
        {
            var client = httpClientFactory.CreateClient("lume-api");

            var body = new CreateCalendarEventRequest(title, start.UtcDateTime, end.UtcDateTime, attendees);
            using var response = await client
                .PostAsJsonAsync($"/api/lume/calendars/{calendarId}/events", body, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Serialize(
                    new { error = $"Lume API returned {(int)response.StatusCode}: {detail}", status = "error" },
                    JsonOptions);
            }

            var result = await response.Content
                .ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message, status = "error" }, JsonOptions);
        }
    }

    private sealed record CreateCalendarEventRequest(
        string Title,
        DateTime StartTimeUtc,
        DateTime EndTimeUtc,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Attendees);
}
