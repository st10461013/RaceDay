using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Image uploads are proxied through the API and persisted in Azure Blob Storage.</summary>
[Route("api")]
public class ImagesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db; private readonly IConfiguration _config;
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
    public ImagesController(RaceDayDbContext db, IConfiguration config) { _db = db; _config = config; }

    [HttpPost("events/{eventId:int}/banner")]
    [SessionAuthorize(UserRole.Organiser)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadBanner(int eventId, IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(_config["AzureBlobStorage:ConnectionString"])) return StatusCode(503, new ProblemDetails { Title = "Image storage unavailable", Detail = "Azure Blob Storage is not configured.", Status = 503 });
        var ev = await _db.Events.FirstOrDefaultAsync(x => x.EventId == eventId);
        if (ev == null) return NotFoundProblem("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only upload images for your own events.");
        var url = await Store(file, "event-banners"); if (url == null) return BadRequestProblem("Choose a PNG, JPG or WebP image up to 5 MB.");
        ev.BannerImageUrl = url; await _db.SaveChangesAsync(); return Ok(new { bannerImageUrl = url });
    }

    [HttpPost("users/me/profile-picture")]
    [SessionAuthorize]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(_config["AzureBlobStorage:ConnectionString"])) return StatusCode(503, new ProblemDetails { Title = "Image storage unavailable", Detail = "Azure Blob Storage is not configured.", Status = 503 });
        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == CurrentUserId);
        if (user == null) return UnauthorizedProblem("Please sign in again.");
        var url = await Store(file, "profile-images"); if (url == null) return BadRequestProblem("Choose a PNG, JPG or WebP image up to 5 MB.");
        user.ProfileImageUrl = url; await _db.SaveChangesAsync(); return Ok(new { profileImageUrl = url });
    }

    private async Task<string?> Store(IFormFile file, string containerName)
    {
        if (file == null || file.Length == 0 || file.Length > 5 * 1024 * 1024 || !Allowed.Contains(file.ContentType)) return null;
        var connection = _config["AzureBlobStorage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Azure Blob Storage is not configured. Set AzureBlobStorage:ConnectionString using user-secrets or environment variables.");
        var service = new BlobServiceClient(connection); var container = service.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
        var ext = file.ContentType switch { "image/jpeg" => ".jpg", "image/png" => ".png", _ => ".webp" };
        var blob = container.GetBlobClient($"{Guid.NewGuid():N}{ext}");
        await using var stream = file.OpenReadStream(); await blob.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType } });
        return blob.Uri.ToString();
    }
}
