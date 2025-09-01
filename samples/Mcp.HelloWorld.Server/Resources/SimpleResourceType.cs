using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mcp.HelloWorld.Server.Resources;

[McpServerResourceType]
public class SimpleResourceType
{
    [McpServerResource(UriTemplate = "test://direct/text/resource", Name = "Direct Text Resource", MimeType = "text/plain")]
    [Description("A direct text resource")]
    public static string DirectTextResource() => "This is a direct resource";

    [McpServerResource(UriTemplate = "test://template/resource/{id}", Name = "Template Resource")]
    [Description("A template resource with a numeric ID")]
    public static ResourceContents TemplateResource(RequestContext<ReadResourceRequestParams> requestContext, int id)
    {
        int index = id - 1;
        if ((uint)index >= ResourceGenerator.Resources.Count)
        {
            throw new NotSupportedException($"Unknown resource: {requestContext.Params?.Uri}");
        }

        var resource = ResourceGenerator.Resources[index];
        return resource.MimeType == "text/plain" ?
            new TextResourceContents
            {
                Text = resource.Description!,
                MimeType = resource.MimeType,
                Uri = resource.Uri,
            } :
            new BlobResourceContents
            {
                Blob = resource.Description!,
                MimeType = resource.MimeType,
                Uri = resource.Uri,
            };
    }
}

static class ResourceGenerator
{
    private static readonly List<Resource> _resources = Enumerable.Range(1, 100).Select(i =>
        {
            var uri = $"test://template/resource/{i}";
            if (i % 2 != 0)
            {
                return new Resource
                {
                    Uri = uri,
                    Name = $"Resource {i}",
                    MimeType = "text/plain",
                    Description = $"Resource {i}: This is a plaintext resource"
                };
            }
            else
            {
                var buffer = System.Text.Encoding.UTF8.GetBytes($"Resource {i}: This is a base64 blob");
                return new Resource
                {
                    Uri = uri,
                    Name = $"Resource {i}",
                    MimeType = "application/octet-stream",
                    Description = Convert.ToBase64String(buffer)
                };
            }
        }).ToList();

    public static IReadOnlyList<Resource> Resources => _resources;
}
