using MinhaGaragem.Domain;

namespace MinhaGaragem.Api;

public sealed class FileStorage
{
    private readonly string root;

    public FileStorage(string root)
    {
        this.root = Path.GetFullPath(root);
        Directory.CreateDirectory(this.root);
    }

    public async Task<(string Key, string ContentType)> Save(
        IFormFile file,
        bool imageOnly,
        CancellationToken ct
    )
    {
        if (
            file.Length is <= 0 or > 8 * 1024 * 1024
            || Path.GetFileName(file.FileName).Length > 255
        )
            throw new DomainException("Envie um arquivo de até 8 MB e nome de até 255 caracteres.");
        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAtLeastAsync(header, Math.Min(12, (int)file.Length), false, ct);
        var type =
            read >= 8
            && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                ? "image/png"
            : read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255 ? "image/jpeg"
            : read >= 12
            && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && header.AsSpan(8, 4).SequenceEqual("WEBP"u8)
                ? "image/webp"
            : read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8) ? "application/pdf"
            : null;
        if (type is null || (imageOnly && type == "application/pdf"))
            throw new DomainException(
                imageOnly ? "Use uma imagem PNG, JPEG ou WebP." : "Use PNG, JPEG, WebP ou PDF."
            );
        var key = Guid.NewGuid().ToString("N");
        try
        {
            await using var output = File.Create(Path.Combine(root, key));
            await output.WriteAsync(header.AsMemory(0, read), ct);
            await input.CopyToAsync(output, ct);
        }
        catch
        {
            Delete(key);
            throw;
        }
        return (key, type);
    }

    public Stream Open(string key)
    {
        var path = SafePath(key);
        if (!File.Exists(path))
            throw new Application.NotFoundException();
        return File.OpenRead(path);
    }

    public void Delete(string key)
    {
        try
        {
            File.Delete(SafePath(key));
        }
        catch (IOException)
        { /* An orphan can be removed by local storage maintenance. */
        }
    }

    private string SafePath(string key) =>
        Guid.TryParseExact(key, "N", out _)
            ? Path.Combine(root, key)
            : throw new DomainException("Arquivo inválido.");
}
