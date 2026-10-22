using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Profiles;

/// <summary>
/// Aggregate root for a deck layout: 1–10 pages with contiguous 0-based indexes, every button inside the grid and
/// at most one button per position per page. Name uniqueness across profiles is a use-case concern.
/// </summary>
public sealed class Profile
{
    public const int MaxPages = 10;

    private readonly List<Page> _pages;

    private Profile(ProfileId id, ProfileName name, KeyColor color, GridSize gridSize, List<Page> pages)
    {
        Id = id;
        Name = name;
        Color = color;
        GridSize = gridSize;
        _pages = pages;
    }

    public ProfileId Id { get; }

    public ProfileName Name { get; private set; }

    public KeyColor Color { get; private set; }

    public GridSize GridSize { get; private set; }

    public IReadOnlyList<Page> Pages => _pages;

    /// <summary>Creates a new profile with a single empty page.</summary>
    public static Profile Create(ProfileName name, KeyColor color, GridSize gridSize) =>
        Restore(ProfileId.New(), name, color, gridSize, [new Page(0, [])]);

    /// <summary>Rebuilds a profile from stored or submitted state, validating every invariant.</summary>
    public static Profile Restore(ProfileId id, ProfileName name, KeyColor color, GridSize gridSize, IEnumerable<Page> pages)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(gridSize);
        ArgumentNullException.ThrowIfNull(pages);
        Invariant.RequireDefined(color, "key color");

        List<Page> pageList = [.. pages];
        Invariant.Require(
            pageList.Count is >= 1 and <= MaxPages,
            DomainErrorCode.ValidationFailed,
            $"A profile has 1–{MaxPages} pages.");
        Invariant.Require(
            pageList.Select(page => page.Index).SequenceEqual(Enumerable.Range(0, pageList.Count)),
            DomainErrorCode.ValidationFailed,
            "Page indexes must be contiguous, ordered and start at 0.");

        var buttons = pageList.SelectMany(page => page.Buttons).ToList();
        Invariant.Require(
            buttons.All(button => gridSize.Contains(button.Position)),
            DomainErrorCode.PositionOutOfGrid,
            "Every button must be inside the grid.");
        Invariant.Require(
            buttons.Select(button => button.Id).Distinct().Count() == buttons.Count,
            DomainErrorCode.ValidationFailed,
            "Button ids must be unique within a profile.");

        return new Profile(id, name, color, gridSize, pageList);
    }

    public void Rename(ProfileName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    public void ChangeColor(KeyColor color)
    {
        Invariant.RequireDefined(color, "key color");
        Color = color;
    }

    /// <summary>Rejects the change with <see cref="DomainErrorCode.GridTooSmall"/> if any button would fall outside.</summary>
    public void ChangeGridSize(GridSize gridSize)
    {
        ArgumentNullException.ThrowIfNull(gridSize);
        Invariant.Require(
            AllButtons().All(button => gridSize.Contains(button.Position)),
            DomainErrorCode.GridTooSmall,
            $"Grid {gridSize.Columns}×{gridSize.Rows} would leave buttons outside.");
        GridSize = gridSize;
    }

    public Page AddPage()
    {
        Invariant.Require(_pages.Count < MaxPages, DomainErrorCode.ValidationFailed, $"A profile has at most {MaxPages} pages.");
        var page = new Page(_pages.Count, []);
        _pages.Add(page);
        return page;
    }

    /// <summary>Removes a page and reindexes the following ones. The last remaining page cannot be removed.</summary>
    public void RemovePage(int pageIndex)
    {
        var page = PageAt(pageIndex);
        Invariant.Require(_pages.Count > 1, DomainErrorCode.ValidationFailed, "A profile keeps at least one page.");

        _pages.Remove(page);
        for (var index = pageIndex; index < _pages.Count; index++)
        {
            _pages[index].Index = index;
        }
    }

    /// <summary>Places a new button on a free position and returns its id.</summary>
    public ButtonId PlaceButton(int pageIndex, GridPosition position, ButtonAppearance appearance)
    {
        var page = PageAt(pageIndex);
        EnsurePlaceable(page, position);

        var button = new Button(ButtonId.New(), position, appearance);
        page.Add(button);
        return button.Id;
    }

    public void ChangeButtonAppearance(ButtonId buttonId, ButtonAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        RequireButton(buttonId).Button.Appearance = appearance;
    }

    public void RemoveButton(ButtonId buttonId)
    {
        var (page, button) = RequireButton(buttonId);
        page.Remove(button);
    }

    /// <summary>
    /// Moves a button within its page. A target held by another button is rejected with
    /// <see cref="DomainErrorCode.PositionOccupied"/> (no swap); moving onto its own position is a no-op.
    /// </summary>
    public void MoveButton(ButtonId buttonId, GridPosition position)
    {
        var (page, button) = RequireButton(buttonId);
        if (button.Position == position)
        {
            return;
        }

        EnsurePlaceable(page, position);
        button.Position = position;
    }

    public Button? FindButton(ButtonId buttonId) => FindWithPage(buttonId)?.Button;

    private IEnumerable<Button> AllButtons() => _pages.SelectMany(page => page.Buttons);

    private Page PageAt(int pageIndex)
    {
        Invariant.Require(
            pageIndex >= 0 && pageIndex < _pages.Count,
            DomainErrorCode.ValidationFailed,
            $"Page {pageIndex} does not exist.");
        return _pages[pageIndex];
    }

    private void EnsurePlaceable(Page page, GridPosition position)
    {
        Invariant.Require(GridSize.Contains(position), DomainErrorCode.PositionOutOfGrid, "Position is outside the grid.");
        Invariant.Require(page.ButtonAt(position) is null, DomainErrorCode.PositionOccupied, "Position is already taken.");
    }

    private (Page Page, Button Button)? FindWithPage(ButtonId buttonId)
    {
        foreach (var page in _pages)
        {
            if (page.Buttons.FirstOrDefault(button => button.Id == buttonId) is { } button)
            {
                return (page, button);
            }
        }

        return null;
    }

    private (Page Page, Button Button) RequireButton(ButtonId buttonId) =>
        FindWithPage(buttonId)
        ?? throw new InvariantViolationException(DomainErrorCode.ButtonNotFound, $"Button {buttonId} does not exist.");
}
