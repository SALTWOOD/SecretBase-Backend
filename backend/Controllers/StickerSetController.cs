using backend.Database.Entities;
using backend.Services;
using backend.Types.Response;
using backend.Types.Responses;
using ImageProxyClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[AllowAnonymous]
[Route("sticker-sets")]
public class StickerSetController(BaseServices deps, IImgproxyClient imgproxyClient) : BaseApiController(deps)
{
    [HttpGet]
    [ProducesResponseType<List<StickerSetResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.StickerSets.AsQueryable();

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StickerSetResponse
            {
                Id = s.Id,
                Name = s.Name,
                StickerCount = s.Stickers.Count,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<StickerSetInfoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var stickerSet = await _db.StickerSets
            .Where(s => s.Id == id)
            .Select(s => new StickerSetInfoResponse
            {
                Id = s.Id,
                Name = s.Name,
                Stickers = s.Stickers.Select(st => new StickerResponse
                {
                    Id = st.Id,
                    Name = st.Name
                }).ToList(),
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (stickerSet == null) return NotFound(new MessageResponse("Sticker set not found."));

        return Ok(stickerSet);
    }

    [HttpGet("{id:int}/details")]
    [ProducesResponseType<StickerUrlResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 18)
    {
        var stickers = _db.Stickers.Where(s => s.StickerSetId == id);
        var totalCount = await stickers.CountAsync();

        var items = await stickers
            .OrderBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var urls = items.Select(s =>
        {
            var url = s.Url;
            Uri uri = new Uri(url);

            if (uri.Scheme.ToLower() != "s3")
                return new StickerUrlResponse(s.Id, s.Name, url);

            string image = imgproxyClient.BuildUrl(url, options =>
                options.Resize(256, 256, ResizeMode.Fit)
                    .Quality(80)
                    .Format(ImageFormat.WebP)
                    .Expires(DateTime.UtcNow.AddHours(1))
            );
            return new StickerUrlResponse(s.Id, s.Name, image);
        });

        Response.Headers.Append("X-Total-Count", totalCount.ToString());
        return Ok(urls);
    }

    [HttpGet("stickers/{stickerId:int}/image")]
    [ProducesResponseType<StickerUrlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStickerImage(int stickerId)
    {
        var sticker = await _db.Stickers.FindAsync(stickerId);
        if (sticker == null) return NotFound(new MessageResponse("Sticker not found."));

        var url = sticker.Url;
        Uri uri = new Uri(url);

        if (uri.Scheme.ToLower() != "s3")
            return Ok(new StickerUrlResponse(sticker.Id, sticker.Name, url));

        var imageUrl = imgproxyClient.BuildUrl(url, options =>
            options.Resize(256, 256, ResizeMode.Fit)
                .Quality(80)
                .Format(ImageFormat.WebP)
                .Expires(DateTime.UtcNow.AddHours(1))
        );

        return Ok(new StickerUrlResponse(sticker.Id, sticker.Name, imageUrl));
    }
}
