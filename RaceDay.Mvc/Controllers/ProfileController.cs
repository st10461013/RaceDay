using Microsoft.AspNetCore.Mvc;using RaceDay.Mvc.Services;using System.Text.Json;
namespace RaceDay.Mvc.Controllers;
public class ProfileController(ApiClient api):Controller{
 public async Task<IActionResult> Index(){if(HttpContext.Session.GetString("User")==null)return RedirectToAction("Login","Auth");var r=await api.Send("GET","api/users/me");return View(r.Data?.RootElement.Clone());}
 [HttpPost] public async Task<IActionResult> Picture(IFormFile picture){if(HttpContext.Session.GetString("User")==null)return RedirectToAction("Login","Auth");if(picture==null||picture.Length==0){TempData["Error"]="Choose an image first.";return RedirectToAction(nameof(Index));}var r=await api.Upload("api/users/me/profile-picture",picture);if(r.Ok)TempData["Success"]="Profile picture updated.";else TempData["Error"]=r.Error;return RedirectToAction(nameof(Index));}
 [HttpPost] public async Task<IActionResult> Update(string firstName,string lastName,string? phoneNumber){var r=await api.Send("PUT","api/users/me",new{firstName,lastName,phoneNumber});if(r.Ok){HttpContext.Session.SetString("User",r.Data!.RootElement.GetRawText());TempData["Success"]="Profile updated.";}else TempData["Error"]=r.Error;return RedirectToAction(nameof(Index));}
}
