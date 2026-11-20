using Microsoft.AspNetCore.Mvc;using RaceDay.Mvc.Models;using RaceDay.Mvc.Services;using System.Text.Json;
namespace RaceDay.Mvc.Controllers;
public class AuthController(ApiClient api):Controller{
 [HttpGet] public IActionResult Login()=>View(new LoginVm());
 [HttpPost] public async Task<IActionResult> Login(LoginVm vm){if(!ModelState.IsValid)return View(vm);var r=await api.Send("POST","api/auth/login",vm);if(!r.Ok){ModelState.AddModelError("",r.Error!);return View(vm);}HttpContext.Session.SetString("User",r.Data!.RootElement.GetRawText());var role=r.Data.RootElement.GetProperty("role").GetString();return RedirectToAction("Index",role=="Organiser"?"Organiser":"Participant");}
 [HttpGet] public IActionResult Register()=>View(new RegisterVm());
 [HttpPost] public async Task<IActionResult> Register(RegisterVm vm){if(!ModelState.IsValid)return View(vm);var r=await api.Send("POST","api/auth/register",vm);if(!r.Ok){ModelState.AddModelError("",r.Error!);return View(vm);}TempData["Success"]="Account created. Please sign in.";return RedirectToAction(nameof(Login));}
 [HttpPost] public async Task<IActionResult> Logout(){await api.Send("POST","api/auth/logout");HttpContext.Session.Clear();return RedirectToAction("Index","Home");}
}
