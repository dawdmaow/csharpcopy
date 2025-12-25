namespace csharpcopy;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.WriteLine("Usage: csharpcopy <source directory> <replica directory> <sync interval in seconds> <log file path>");
            return 1;
        }

        var sourceDir = args[0];
        var replicaDir = args[1];
        var logFilePath = args[3];

        if (string.IsNullOrEmpty(sourceDir))
        {
            Console.WriteLine("ERROR: Source directory cannot be empty");
            return 1;
        }

        if (string.IsNullOrEmpty(replicaDir))
        {
            Console.WriteLine("ERROR: Replica directory cannot be empty");
            return 1;
        }

        if (!int.TryParse(args[2], out var intervalSeconds) || intervalSeconds <= 0)
        {
            Console.WriteLine("ERROR: Sync interval must be a positive integer (seconds)");
            return 1;
        }

        if (string.IsNullOrEmpty(logFilePath))
        {
            Console.WriteLine("ERROR: Log file path cannot be empty");
            return 1;
        }

        using var logger = new Logger(logFilePath);
        var synchronizer = new DirectorySynchronizer(sourceDir, replicaDir, logger);
        using var timer = new System.Timers.Timer(intervalSeconds * 1000);

        try
        {
            logger.Log($"Directory synchronization started");
            logger.Log($"Source: {sourceDir}");
            logger.Log($"Replica: {replicaDir}");
            logger.Log($"Interval: {intervalSeconds} seconds");
            logger.Log($"Log file: {logFilePath}");

            synchronizer.SynchronizeAll();

            timer.Elapsed += (sender, e) =>
            {
                try
                {
                    synchronizer.SynchronizeAll();
                }
                catch (Exception ex)
                {
                    logger.Log($"ERROR: Synchronization failed: {ex.Message}");
                }
            };
            timer.AutoReset = true;
            timer.Start();

            Console.WriteLine("Press Ctrl+C to stop synchronization...");

            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                exitEvent.Set();
            };

            exitEvent.WaitOne();
        }
        catch (Exception ex)
        {
            logger.Log($"FATAL ERROR: {ex}");
            return 1;
        }
        finally
        {
            timer.Stop();
        }

        Console.WriteLine("Directory synchronization stopped");
        return 0;
    }
}
