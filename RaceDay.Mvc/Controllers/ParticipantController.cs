using Microsoft.AspNetCore.Mvc;using RaceDay.Mvc.Services;using System.Text.Json;
namespace RaceDay.Mvc.Controllers;
public class ParticipantController(ApiClient api):Controller{
 private bool Guard()=>HttpContext.Session.GetString("User") is string u&&JsonDocument.Parse(u).RootElement.GetProperty("role").GetString()=="Participant";private IActionResult Deny()=>RedirectToAction("Login","Auth");
 public async Task<IActionResult> Index(){if(!Guard())return Deny();var r=await api.Send("GET","api/events");return View(r.Data?.RootElement.Clone());}
 public async Task<IActionResult> Details(int id){if(!Guard())return Deny();var e=await api.Send("GET",$"api/events/{id}");var c=await api.Send("GET",$"api/events/{id}/categories");ViewBag.Categories=c.Data?.RootElement.Clone();ViewBag.Error=e.Error??c.Error;return View(e.Data?.RootElement.Clone());}
 [HttpPost] public async Task<IActionResult> Enrol(int id,int eventId){if(!Guard())return Deny();var r=await api.Send("POST",$"api/categories/{id}/enrolments",new{});TempData[r.Ok?"Success":"Error"]=r.Ok?"Your enrolment is confirmed.":r.Error;return RedirectToAction(nameof(Details),new{id=eventId});}
 public async Task<IActionResult> MyEvents(){if(!Guard())return Deny();var r=await api.Send("GET","api/enrolments/me");return View(r.Data?.RootElement.Clone());}
 public async Task<IActionResult> Results(){if(!Guard())return Deny();var r=await api.Send("GET","api/results/me");return View(r.Data?.RootElement.Clone());}
}
