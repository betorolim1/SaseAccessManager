using Microsoft.AspNetCore.Mvc.RazorPages;
using SaseAccessManager.Models;
using SaseAccessManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace SaseAccessManager.Pages.Users;

public class IndexModel : PageModel
{
    private readonly FileUserStore _store;
    private readonly UserService _service;

    public List<TemporarySaseUser> Users { get; set; } = new();

    public IndexModel(FileUserStore store, UserService service)
    {
        _store = store;
        _service = service;
    }

    public async Task OnGet()
    {
        Users = (await _store.GetAll())
        .OrderByDescending(u => u.CreatedAt)
        .ToList();
    }

    public async Task<IActionResult> OnPostRemove(string id)
    {
        await _service.Remove(id);
        return RedirectToPage();
    }
}