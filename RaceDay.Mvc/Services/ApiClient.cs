using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace RaceDay.Mvc.Services;
public class ApiClient
{
 private readonly IHttpClientFactory _factory; private readonly IHttpContextAccessor _accessor;
 private static readonly JsonSerializerOptions Json = new(){PropertyNameCaseInsensitive=true};
 public ApiClient(IHttpClientFactory factory, IHttpContextAccessor accessor){_factory=factory;_accessor=accessor;}
 private HttpClient Client(){var c=_factory.CreateClient("RaceDayApi"); var cookie=_accessor.HttpContext?.Session.GetString("ApiCookie"); if(!string.IsNullOrWhiteSpace(cookie)) c.DefaultRequestHeaders.Add("Cookie",cookie); return c;}
 public async Task<(bool Ok, JsonDocument? Data, string? Error)> Send(string method,string path,object? body=null){using var c=Client(); using var req=new HttpRequestMessage(new HttpMethod(method),path); if(body!=null) req.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json"); using var res=await c.SendAsync(req); var text=await res.Content.ReadAsStringAsync(); if(!res.IsSuccessStatusCode)return(false,null,ExtractError(text,(int)res.StatusCode)); if(method.Equals("POST",StringComparison.OrdinalIgnoreCase)&&path.EndsWith("/login")){var sc=res.Headers.TryGetValues("Set-Cookie",out var vals)?vals.FirstOrDefault():null; if(sc!=null)_accessor.HttpContext?.Session.SetString("ApiCookie",sc.Split(';')[0]);} if(string.IsNullOrWhiteSpace(text))return(true,null,null); try{return(true,JsonDocument.Parse(text),null);}catch{return(true,null,null);} }
 public async Task<(bool Ok, JsonDocument? Data, string? Error)> Upload(string path,IFormFile file){using var c=Client();using var form=new MultipartFormDataContent();await using var stream=file.OpenReadStream();using var content=new StreamContent(stream);content.Headers.ContentType=new MediaTypeHeaderValue(file.ContentType);form.Add(content,"file",Path.GetFileName(file.FileName));using var res=await c.PostAsync(path,form);var text=await res.Content.ReadAsStringAsync();if(!res.IsSuccessStatusCode)return(false,null,ExtractError(text,(int)res.StatusCode));return(true,string.IsNullOrWhiteSpace(text)?null:JsonDocument.Parse(text),null);}
 public static string ExtractError(string text,int status){try{using var d=JsonDocument.Parse(text);if(d.RootElement.TryGetProperty("detail",out var detail))return detail.GetString()??$"Request failed ({status}).";if(d.RootElement.TryGetProperty("title",out var title))return title.GetString()??$"Request failed ({status}).";if(d.RootElement.TryGetProperty("errors",out var errors))return string.Join(" ",errors.EnumerateObject().SelectMany(x=>x.Value.EnumerateArray()).Select(x=>x.GetString()));}catch{}return $"Request failed ({status}). Check the information and try again.";}
}
