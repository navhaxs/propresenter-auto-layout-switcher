namespace Logic;

public static class JSONUtil
{
    public static string StreamToString(Stream stream)
    {
        stream.Flush();
        stream.Seek(0, SeekOrigin.Begin);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}