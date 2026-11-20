using Microsoft.AspNetCore.Mvc;using RaceDay.Mvc.Services;using System.Text.Json;
namespace RaceDay.Mvc.Controllers;
public class HomeController(ApiClient api):Controller{
 public async Task<IActionResult> Index(string? search,string? type){var r=await api.Send("GET","api/events?"+string.Join("&",new[]{!string.IsNullOrWhiteSpace(search)?"search="+Uri.EscapeDataString(search):null,!string.IsNullOrWhiteSpace(type)?"type="+Uri.EscapeDataString(type):null}.Where(x=>x!=null)));ViewBag.Error=r.Error;ViewBag.Search=search;ViewBag.Type=type;return View(r.Data?.RootElement.Clone());}
 public IActionResult Error()=>View();
}
