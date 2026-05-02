namespace HomeGuard.Client.Model;

public record CapturedDocument(
    byte[]  Data,
    string  MimeType,
    string  FileName
);
