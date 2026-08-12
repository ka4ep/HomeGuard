namespace HomeGuard.Client;

/// <summary>
/// Marker type for <c>IStringLocalizer&lt;Strings&gt;</c>. It carries no members —
/// its only job is to name the resource set, which the localizer resolves to
/// <c>Resources/Strings.resx</c> (and <c>Strings.ru.resx</c> beside it).
/// <para>
/// Inject it as <c>@inject IStringLocalizer&lt;Strings&gt; L</c> and read keys with
/// <c>L["Area_Thing"]</c>. A missing key renders as the key itself, so a gap is
/// visible on screen and greppable in the source rather than throwing.
/// </para>
/// </summary>
public sealed class Strings;
