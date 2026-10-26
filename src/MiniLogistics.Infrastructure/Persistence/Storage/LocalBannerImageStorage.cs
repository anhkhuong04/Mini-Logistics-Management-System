using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application.Banners;
using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;
namespace MiniLogistics.Infrastructure.Persistence.Storage;
public sealed class BannerImageStorageOptions { public string RootPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiniLogistics","banner-images"); }
public sealed class LocalBannerImageStorage(IOptions<BannerImageStorageOptions> options,ILogger<LocalBannerImageStorage> logger) : IBannerImageStorage
{
    public const int MaxBytes=5*1024*1024;
    private static readonly Dictionary<string,string> Types=new(StringComparer.OrdinalIgnoreCase){{".png","image/png"},{".jpg","image/jpeg"},{".jpeg","image/jpeg"},{".webp","image/webp"},{".gif","image/gif"},{".svg","image/svg+xml"}};
    public async Task<Result<BannerImageResponse>> SaveAsync(Stream content,string fileName,string contentType,CancellationToken cancellationToken) {
        var extension=Path.GetExtension(fileName??string.Empty).ToLowerInvariant();
        if(string.IsNullOrWhiteSpace(fileName)||fileName.Length>255||fileName.Contains('/')||fileName.Contains('\\')||fileName.Contains(':')||fileName.Any(char.IsControl)||!Types.TryGetValue(extension,out var mime)||!string.Equals(mime,contentType,StringComparison.OrdinalIgnoreCase)) return Invalid();
        string? destination=null;
        try {
            using var bytes=new MemoryStream(); var buffer=new byte[81920]; int read;
            while((read=await content.ReadAsync(buffer,cancellationToken))>0){ if(bytes.Length+read>MaxBytes)return Invalid(); await bytes.WriteAsync(buffer.AsMemory(0,read),cancellationToken); }
            var data=bytes.ToArray(); if(!ValidContent(extension,data))return Invalid();
            var root=Path.GetFullPath(options.Value.RootPath); Directory.CreateDirectory(root);
            var name=$"banner_{Guid.NewGuid():N}{extension}"; destination=Path.Combine(root,name);
            await using(var file=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous))await file.WriteAsync(data,cancellationToken);
            destination=null; return Result<BannerImageResponse>.Success(new($"/images/banners/{name}"));
        } catch(OperationCanceledException){throw;} catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){logger.LogError(exception,"Banner image storage failed.");return Result<BannerImageResponse>.Failure(new Error("Application.Unavailable","Image storage unavailable."));}
        finally { if(destination is not null) { try{File.Delete(destination);}catch(IOException){}catch(UnauthorizedAccessException){} } }
    }
    private static Result<BannerImageResponse> Invalid()=>Result<BannerImageResponse>.Failure(ApplicationErrors.ValidationFailed("Invalid image, filename, type or size."));
    private static bool ValidContent(string extension,byte[] data) {
        if(data.Length==0)return false;
        return extension switch {
            ".png"=>data.Length>=33&&data.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})&&data.AsSpan(12,4).SequenceEqual("IHDR"u8),
            ".jpg" or ".jpeg"=>data.Length>=4&&data[0]==255&&data[1]==216&&data[^2]==255&&data[^1]==217,
            ".gif"=>data.Length>=14&&(data.AsSpan(0,6).SequenceEqual("GIF87a"u8)||data.AsSpan(0,6).SequenceEqual("GIF89a"u8))&&data[^1]==59,
            ".webp"=>data.Length>=20&&data.AsSpan(0,4).SequenceEqual("RIFF"u8)&&data.AsSpan(8,4).SequenceEqual("WEBP"u8)&&(data.AsSpan(12,4).SequenceEqual("VP8 "u8)||data.AsSpan(12,4).SequenceEqual("VP8L"u8)||data.AsSpan(12,4).SequenceEqual("VP8X"u8)),
            ".svg"=>SafeSvg(data), _=>false };
    }
    private static bool SafeSvg(byte[] data) {
        try {
            using var source=new MemoryStream(data); using var reader=XmlReader.Create(source,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaxBytes});
            var document=XDocument.Load(reader); if(document.Root?.Name!=XName.Get("svg","http://www.w3.org/2000/svg") || document.DescendantNodes().OfType<XProcessingInstruction>().Any())return false;
            var elements=new HashSet<string>{"svg","g","path","rect","circle","ellipse","line","polyline","polygon","text","tspan","title","desc","defs","clipPath","linearGradient","radialGradient","stop"};
            var attributes=new HashSet<string>{"id","viewBox","width","height","x","y","x1","x2","y1","y2","cx","cy","r","rx","ry","d","points","fill","fill-opacity","fill-rule","stroke","stroke-width","stroke-opacity","stroke-linecap","stroke-linejoin","opacity","transform","offset","stop-color","stop-opacity","font-size","font-family","text-anchor","preserveAspectRatio","xmlns"};
            return document.Root.DescendantsAndSelf().All(e=>e.Name.NamespaceName=="http://www.w3.org/2000/svg"&&elements.Contains(e.Name.LocalName)&&e.Attributes().All(a=>a.IsNamespaceDeclaration
                ?a.Value=="http://www.w3.org/2000/svg":a.Name.NamespaceName.Length==0&&attributes.Contains(a.Name.LocalName)&&!a.Value.Contains("url",StringComparison.OrdinalIgnoreCase)&&!a.Value.Contains("javascript",StringComparison.OrdinalIgnoreCase)));
        } catch(XmlException){return false;}
    }
}
