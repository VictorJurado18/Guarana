using System.Globalization;

namespace Guarana.Core;

public static class ScheduleEngine
{
    private const string TimeFormat = "HH:mm";

    public static void Validate(ScheduleRule rule)
    {
        if (!Enum.IsDefined(rule.Day) || !TryParse(rule.Start, out _) || !TryParse(rule.End, out _))
            throw new ArgumentException("Use um dia válido e horários no formato HH:mm.");
    }

    public static bool TryParse(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out time);

    public static bool IsActive(DateTime localNow, IReadOnlyList<ScheduleRule> rules)
    {
        var date = localNow.Date;
        foreach (var rule in rules)
        {
            if (!TryParse(rule.Start, out var start) || !TryParse(rule.End, out var end))
                continue;
            for (int offset = -1; offset <= 0; offset++)
            {
                var startDate = date.AddDays(offset);
                if (startDate.DayOfWeek != rule.Day) continue;
                var begin = startDate.Add(start.ToTimeSpan());
                var finish = startDate.Add(end.ToTimeSpan());
                if (finish <= begin) finish = finish.AddDays(1);
                if (localNow >= begin && localNow < finish) return true;
            }
        }
        return false;
    }

    public static DateTime? NextTransition(DateTime localNow, IReadOnlyList<ScheduleRule> rules)
    {
        var boundaries = new SortedSet<DateTime>();
        for (int offset = -1; offset <= 8; offset++)
        {
            var date = localNow.Date.AddDays(offset);
            foreach (var rule in rules)
            {
                if (rule.Day != date.DayOfWeek ||
                    !TryParse(rule.Start, out var start) ||
                    !TryParse(rule.End, out var end)) continue;
                var begin = date.Add(start.ToTimeSpan());
                var finish = date.Add(end.ToTimeSpan());
                if (finish <= begin) finish = finish.AddDays(1);
                if (begin > localNow) boundaries.Add(begin);
                if (finish > localNow) boundaries.Add(finish);
            }
        }
        foreach (var boundary in boundaries)
            if (IsActive(boundary.AddTicks(-1), rules) != IsActive(boundary, rules))
                return boundary;
        return null;
    }
}
