namespace ThucLuc.Application.Common.Models;

public class PagedQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? SortBy { get; set; }

    public string? SortDir { get; set; }
}