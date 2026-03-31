using System.Net;
using System.Net.Mime;
using Amazon.S3;
using Amazon.S3.Util;
using backend.Controllers.Admin;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("storage")]
[Produces(MediaTypeNames.Application.Json)]
public class StorageController(BaseServices deps, IAmazonS3 s3Client) : BaseApiController(deps)
{
    [HttpGet("direct")]
    [ProducesResponseType<UrlResponse>(StatusCodes.Status200OK)]
    public IActionResult GetDirectLink([FromQuery] string s3Url)
    {
        var uri = new AmazonS3Uri(s3Url);
        var baseUrl = s3Client.Config.ServiceURL.TrimEnd('/');
    
        var encodedKey = string.Join("/", 
            uri.Key.Split('/').Select(part => WebUtility.UrlEncode(part)));
        var url = $"{baseUrl}/{uri.Bucket}/{encodedKey}";
        return Ok(new UrlResponse(url));
    }
}