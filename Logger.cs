using System.Text;

namespace csharpcopy;

public class Logger : IDisposable
{
    private readonly StreamWriter _fileWriter;
    private readonly Lock _lock = new();

    public Logger(string logFilePath)
    {
        // Consider throwing on an empty logDirectory right away.
        var logDirectory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(logDirectory))
        {
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
        }

        _fileWriter = new StreamWriter(logFilePath, append: true, Encoding.UTF8);
    }

    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logMessage = $"[{timestamp}] {message}";

        lock (_lock)
        {
            Console.WriteLine(logMessage);
            _fileWriter.WriteLine(logMessage);
            _fileWriter.Flush();
        }
    }

    public void Dispose()
    {
        _fileWriter?.Dispose();
        GC.SuppressFinalize(this);
    }
}

